using Application.Common.Interfaces.Queries.Monitoring;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;
using Domain.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class RawMeasurementQueries(ApplicationDbContext context) : IRawMeasurementQueries
{
    public Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesAsync(
        Guid pollutantId,
        Guid emissionSourceId,
        DateTime from,
        DateTime to,
        BucketWindow window,
        AggregationFunc aggregation,
        CancellationToken cancellationToken) =>
        aggregation == AggregationFunc.P95
            ? GetTimeSeriesFromRawAsync(pollutantId, emissionSourceId, from, to, window, cancellationToken)
            : GetTimeSeriesFromCaAsync(pollutantId, emissionSourceId, from, to, window, aggregation, cancellationToken);

    public Task<IReadOnlyList<HeatmapPoint>> GetHeatmapAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken,
        BoundingBox? bbox = null) =>
        aggregation == AggregationFunc.P95
            ? GetHeatmapFromRawAsync(pollutantId, from, to, bbox, cancellationToken)
            : GetHeatmapFromCaAsync(pollutantId, from, to, aggregation, bbox, cancellationToken);

    public async Task<decimal?> GetHeatmapScaleAsync(
        Guid pollutantId,
        DateTime from,
        DateTime to,
        AggregationFunc aggregation,
        CancellationToken cancellationToken)
    {
        // Full tenant set (no bbox) so the colour scale doesn't shift as the viewport changes.
        var points = await GetHeatmapAsync(pollutantId, from, to, aggregation, cancellationToken);
        if (points.Count == 0) return null;
        var values = points.Select(p => p.Value).OrderBy(v => v).ToList();
        return Percentile(values, 0.98);
    }

    // Mirrors the frontend percentile (shared/lib/heatmap-gradient.ts): nearest-rank with
    // idx = floor(count * p), clamped to [0, count-1]. `sorted` must be ascending.
    private static decimal Percentile(IReadOnlyList<decimal> sorted, double p)
    {
        if (sorted.Count == 0) return 0m;
        var idx = (int)Math.Floor(sorted.Count * p);
        idx = Math.Min(sorted.Count - 1, Math.Max(0, idx));
        return sorted[idx];
    }

    // ─── Compliance audit (what-if read-only) ────────────────────────────────────

    public async Task<ComplianceAuditResult?> GetComplianceAuditAsync(
        ComplianceAuditQueryParams query, CancellationToken cancellationToken)
    {
        var period = PeriodToTimeSpan(query.Period);
        if (period == TimeSpan.Zero) return null;

        var periodLiteral = PeriodToPgInterval(period);

        // Pull per-(window, unit_id) slices: leaves canonical conversion + limit comparison to
        // C# so a device-swap that changed units mid-period folds into a single honest value
        // per bucket. Same approach the materializer uses (Phase 2).
        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{periodLiteral}', m.bucket) AS window_start,
                m.unit_id,
                SUM(m.sum_value)::numeric(18,6) AS sum_value,
                SUM(m.sample_count)::int AS sample_count
            FROM measurement_1m m
            WHERE m.emission_source_id = @source_id
              AND m.pollutant_id = @pollutant_id
              AND m.bucket >= @from
              AND m.bucket < @to
            GROUP BY window_start, m.unit_id
            ORDER BY window_start, m.unit_id";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "source_id", NpgsqlDbType.Uuid, query.EmissionSourceId);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, query.PollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(query.From));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(query.To));

        var slices = new List<AuditSlice>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                slices.Add(new AuditSlice(
                    WindowStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                    UnitId: reader.GetGuid(1),
                    SumValue: reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                    SampleCount: reader.GetInt32(3)));
            }
        }

        var ctx = await LoadCanonicalConversionContextAsync(
            query.PollutantId,
            slices.Select(s => s.UnitId).Append(query.LimitUnitId),
            cancellationToken);
        if (ctx is null) return null;
        if (!ctx.Units.TryGetValue(query.LimitUnitId, out var limitUnit)) return null;

        // Limit → canonical. Failure (e.g. limit is MassFlow but pollutant canonical is
        // MassConcentration) means we can't compare apples to apples; surface null to the
        // caller rather than fabricating a misleading result.
        if (!UnitConverter.TryToCanonical(
                query.LimitValue, limitUnit, ctx.CanonicalUnit, ctx.MolarMass,
                out var limitCanonical, out _))
        {
            return null;
        }

        long bucketsWithData = 0;
        long exceedanceBuckets = 0;
        decimal? maxValue = null;
        decimal sumOfBucketAvgs = 0m;
        long bucketCountForAvg = 0;
        decimal? maxRatio = null;

        foreach (var bucket in slices.GroupBy(s => s.WindowStart))
        {
            decimal canonicalSumOfSums = 0m;
            var canonicalSampleCount = 0;
            foreach (var s in bucket)
            {
                if (!ctx.Units.TryGetValue(s.UnitId, out var fromUnit)) continue;
                if (!UnitConverter.TryToCanonical(
                        s.SumValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                        out var canonicalSum, out _)) continue;
                canonicalSumOfSums += canonicalSum;
                canonicalSampleCount += s.SampleCount;
            }
            if (canonicalSampleCount == 0) continue;

            var canonicalBucketAvg = canonicalSumOfSums / canonicalSampleCount;
            bucketsWithData++;
            sumOfBucketAvgs += canonicalBucketAvg;
            bucketCountForAvg++;

            if (canonicalBucketAvg > limitCanonical) exceedanceBuckets++;
            if (maxValue is null || canonicalBucketAvg > maxValue) maxValue = canonicalBucketAvg;
            if (limitCanonical > 0m)
            {
                var ratio = canonicalBucketAvg / limitCanonical;
                if (maxRatio is null || ratio > maxRatio) maxRatio = ratio;
            }
        }

        var avgValue = bucketCountForAvg == 0
            ? (decimal?)null
            : Math.Round(sumOfBucketAvgs / bucketCountForAvg, 6);

        var totalBuckets = (int)Math.Max(0, (query.To - query.From).Ticks / period.Ticks);
        var dataAvailability = totalBuckets == 0
            ? 0m
            : Math.Round((decimal)bucketsWithData / totalBuckets, 4);
        var exceedanceRate = bucketsWithData == 0
            ? (decimal?)null
            : Math.Round((decimal)exceedanceBuckets / bucketsWithData, 4);

        return new ComplianceAuditResult(
            From: EnsureUtc(query.From),
            To: EnsureUtc(query.To),
            Period: query.Period,
            // "InBase" historically meant "in the dimension base unit of the limit"; after Phase
            // 5b the comparison happens in the pollutant's canonical unit instead. For built-in
            // MassConcentration pollutants the canonical IS the dimension base, so consumer-facing
            // numbers are unchanged for the common case.
            LimitValueInBase: limitCanonical,
            LimitUnitSymbol: ctx.CanonicalUnit.Symbol,
            TotalBuckets: totalBuckets,
            BucketsWithData: bucketsWithData,
            ExceedanceBuckets: exceedanceBuckets,
            MaxValueInBase: maxValue is null ? null : Math.Round(maxValue.Value, 6),
            AvgValueInBase: avgValue,
            MaxRatio: maxRatio is null ? null : Math.Round(maxRatio.Value, 6),
            ExceedanceRate: exceedanceRate,
            DataAvailability: dataAvailability);
    }

    private record AuditSlice(DateTime WindowStart, Guid UnitId, decimal SumValue, int SampleCount);

    private static TimeSpan PeriodToTimeSpan(AveragingWindow period) => period switch
    {
        AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
        AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
        AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
        AveragingWindow.Hour1 => TimeSpan.FromHours(1),
        AveragingWindow.Hour24 => TimeSpan.FromHours(24),
        _ => TimeSpan.Zero
    };

    private static string PeriodToPgInterval(TimeSpan period)
    {
        if (period == TimeSpan.FromMinutes(1)) return "1 minute";
        if (period == TimeSpan.FromMinutes(10)) return "10 minutes";
        if (period == TimeSpan.FromMinutes(30)) return "30 minutes";
        if (period == TimeSpan.FromHours(1)) return "1 hour";
        if (period == TimeSpan.FromHours(24)) return "1 day";
        throw new ArgumentOutOfRangeException(nameof(period), period, null);
    }

    // ─── Continuous-aggregate path (avg/max/min/sum) ────────────────────────────

    private async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesFromCaAsync(
        Guid pollutantId, Guid emissionSourceId, DateTime from, DateTime to,
        BucketWindow window, AggregationFunc aggregation, CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var tenantClause = BuildTenantClause();

        // Per-(bucket, unit_id) row. C# converts each slice into the pollutant's canonical unit
        // via UnitConverter then folds slices per bucket — same approach the materializer uses
        // so the displayed value stays consistent across device-swaps that change units.
        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{bucketLiteral}', m.bucket) AS bucket_start,
                m.unit_id,
                SUM(m.sum_value)::numeric(18,6) AS sum_value,
                MIN(m.min_value)::numeric(18,6) AS min_value,
                MAX(m.max_value)::numeric(18,6) AS max_value,
                SUM(m.sample_count)::int AS sample_count,
                SUM(m.valid_count)::int AS valid_count
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.pollutant_id = @pollutant_id
              AND m.emission_source_id = @emission_source_id
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY bucket_start, m.unit_id
            ORDER BY bucket_start, m.unit_id";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "emission_source_id", NpgsqlDbType.Uuid, emissionSourceId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        var slices = new List<TimeSeriesSlice>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                slices.Add(new TimeSeriesSlice(
                    BucketStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                    UnitId: reader.GetGuid(1),
                    SumValue: reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                    MinValue: reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    MaxValue: reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                    SampleCount: reader.GetInt32(5),
                    ValidCount: reader.GetInt32(6)));
            }
        }
        if (slices.Count == 0) return [];

        var ctx = await LoadCanonicalConversionContextAsync(
            pollutantId, slices.Select(s => s.UnitId), cancellationToken);
        if (ctx is null) return [];

        return slices
            .GroupBy(s => s.BucketStart)
            .Select(g => FoldTimeSeriesBucket(g, aggregation, ctx))
            .Where(p => p is not null)
            .Select(p => p!)
            .OrderBy(p => p.BucketStart)
            .ToList();
    }

    private async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapFromCaAsync(
        Guid pollutantId, DateTime from, DateTime to,
        AggregationFunc aggregation, BoundingBox? bbox, CancellationToken cancellationToken)
    {
        var tenantClause = BuildTenantClause();
        var bboxClause = bbox is null
            ? string.Empty
            : "AND ST_Intersects(es.location, ST_MakeEnvelope(@min_lng, @min_lat, @max_lng, @max_lat, 4326))";

        var sql = $@"
            SELECT
                es.id AS emission_source_id,
                ST_Y(es.location) AS latitude,
                ST_X(es.location) AS longitude,
                m.unit_id,
                SUM(m.sum_value)::numeric(18,6) AS sum_value,
                MIN(m.min_value)::numeric(18,6) AS min_value,
                MAX(m.max_value)::numeric(18,6) AS max_value,
                SUM(m.sample_count)::int AS sample_count,
                SUM(m.valid_count)::int AS valid_count
            FROM measurement_1m m
            JOIN emission_source es ON es.id = m.emission_source_id
            WHERE m.pollutant_id = @pollutant_id
              AND m.bucket >= @from
              AND m.bucket < @to
              AND es.deleted_at IS NULL
              {tenantClause}
              {bboxClause}
            GROUP BY es.id, es.location, m.unit_id
            ORDER BY es.id, m.unit_id";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);
        AddBboxParams(command, bbox);

        var slices = new List<HeatmapSlice>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                slices.Add(new HeatmapSlice(
                    EmissionSourceId: reader.GetGuid(0),
                    Latitude: reader.GetDouble(1),
                    Longitude: reader.GetDouble(2),
                    UnitId: reader.GetGuid(3),
                    SumValue: reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                    MinValue: reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                    MaxValue: reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                    SampleCount: reader.GetInt32(7),
                    ValidCount: reader.GetInt32(8)));
            }
        }
        if (slices.Count == 0) return [];

        var ctx = await LoadCanonicalConversionContextAsync(
            pollutantId, slices.Select(s => s.UnitId), cancellationToken);
        if (ctx is null) return [];

        return slices
            .GroupBy(s => s.EmissionSourceId)
            .Select(g => FoldHeatmapSource(g, aggregation, ctx))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    // ─── Canonical conversion fold ──────────────────────────────────────────────

    private record TimeSeriesSlice(
        DateTime BucketStart, Guid UnitId,
        decimal SumValue, decimal MinValue, decimal MaxValue,
        int SampleCount, int ValidCount);

    private record HeatmapSlice(
        Guid EmissionSourceId, double Latitude, double Longitude, Guid UnitId,
        decimal SumValue, decimal MinValue, decimal MaxValue,
        int SampleCount, int ValidCount);

    private record ConversionContext(
        MeasureUnit CanonicalUnit, decimal? MolarMass,
        IReadOnlyDictionary<Guid, MeasureUnit> Units);

    /// <summary>
    /// Loads the pollutant's canonical MeasureUnit + MolarMass and every involved unit. Returns
    /// null when the pollutant or its canonical unit can't be loaded — caller should treat that
    /// as "no data" rather than throwing, because a missing pollutant config shouldn't crash
    /// the chart endpoint.
    /// </summary>
    private async Task<ConversionContext?> LoadCanonicalConversionContextAsync(
        Guid pollutantId, IEnumerable<Guid> involvedUnitIds, CancellationToken ct)
    {
        var pollutant = await context.Set<Pollutant>().AsNoTracking()
            .Where(p => p.Id == pollutantId)
            .Select(p => new { p.CanonicalUnitId, p.MolarMass })
            .FirstOrDefaultAsync(ct);
        if (pollutant is null) return null;

        var allUnitIds = involvedUnitIds
            .Append(pollutant.CanonicalUnitId)
            .Distinct()
            .ToArray();
        var units = await context.Set<MeasureUnit>().AsNoTracking()
            .Where(u => allUnitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        if (!units.TryGetValue(pollutant.CanonicalUnitId, out var canonicalUnit)) return null;

        return new ConversionContext(canonicalUnit, pollutant.MolarMass, units);
    }

    private static TimeSeriesPoint? FoldTimeSeriesBucket(
        IEnumerable<TimeSeriesSlice> slices, AggregationFunc aggregation, ConversionContext ctx)
    {
        decimal? value = null;
        var totalCount = 0;
        var validCount = 0;
        decimal canonicalSumOfSums = 0m;
        var canonicalSumSamples = 0;
        decimal? canonicalMin = null;
        decimal? canonicalMax = null;
        decimal canonicalSum = 0m;
        var anySliceConverted = false;

        foreach (var s in slices)
        {
            totalCount += s.SampleCount;
            validCount += s.ValidCount;
            if (!ctx.Units.TryGetValue(s.UnitId, out var fromUnit)) continue;
            if (!UnitConverter.TryToCanonical(s.SumValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                    out var canonicalSliceSum, out _)) continue;
            UnitConverter.TryToCanonical(s.MinValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                out var canonicalSliceMin, out _);
            UnitConverter.TryToCanonical(s.MaxValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                out var canonicalSliceMax, out _);

            anySliceConverted = true;
            canonicalSumOfSums += canonicalSliceSum;
            canonicalSumSamples += s.SampleCount;
            canonicalSum += canonicalSliceSum;
            canonicalMin = canonicalMin is null ? canonicalSliceMin : Math.Min(canonicalMin.Value, canonicalSliceMin);
            canonicalMax = canonicalMax is null ? canonicalSliceMax : Math.Max(canonicalMax.Value, canonicalSliceMax);
        }

        if (!anySliceConverted) return null;

        value = aggregation switch
        {
            AggregationFunc.Average => canonicalSumSamples > 0
                ? Math.Round(canonicalSumOfSums / canonicalSumSamples, 6)
                : 0m,
            AggregationFunc.Max => canonicalMax ?? 0m,
            AggregationFunc.Min => canonicalMin ?? 0m,
            AggregationFunc.Sum => canonicalSum,
            _ => 0m
        };

        return new TimeSeriesPoint(
            BucketStart: slices.First().BucketStart,
            Value: value.Value,
            TotalPointsCount: totalCount,
            ValidPointsCount: validCount);
    }

    private static HeatmapPoint? FoldHeatmapSource(
        IEnumerable<HeatmapSlice> slices, AggregationFunc aggregation, ConversionContext ctx)
    {
        var first = slices.First();
        var totalCount = 0;
        var validCount = 0;
        decimal canonicalSumOfSums = 0m;
        var canonicalSumSamples = 0;
        decimal? canonicalMin = null;
        decimal? canonicalMax = null;
        decimal canonicalSum = 0m;
        var anySliceConverted = false;

        foreach (var s in slices)
        {
            totalCount += s.SampleCount;
            validCount += s.ValidCount;
            if (!ctx.Units.TryGetValue(s.UnitId, out var fromUnit)) continue;
            if (!UnitConverter.TryToCanonical(s.SumValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                    out var canonicalSliceSum, out _)) continue;
            UnitConverter.TryToCanonical(s.MinValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                out var canonicalSliceMin, out _);
            UnitConverter.TryToCanonical(s.MaxValue, fromUnit, ctx.CanonicalUnit, ctx.MolarMass,
                out var canonicalSliceMax, out _);

            anySliceConverted = true;
            canonicalSumOfSums += canonicalSliceSum;
            canonicalSumSamples += s.SampleCount;
            canonicalSum += canonicalSliceSum;
            canonicalMin = canonicalMin is null ? canonicalSliceMin : Math.Min(canonicalMin.Value, canonicalSliceMin);
            canonicalMax = canonicalMax is null ? canonicalSliceMax : Math.Max(canonicalMax.Value, canonicalSliceMax);
        }

        if (!anySliceConverted) return null;

        var value = aggregation switch
        {
            AggregationFunc.Average => canonicalSumSamples > 0
                ? Math.Round(canonicalSumOfSums / canonicalSumSamples, 6)
                : 0m,
            AggregationFunc.Max => canonicalMax ?? 0m,
            AggregationFunc.Min => canonicalMin ?? 0m,
            AggregationFunc.Sum => canonicalSum,
            _ => 0m
        };

        return new HeatmapPoint(
            EmissionSourceId: first.EmissionSourceId,
            Latitude: first.Latitude,
            Longitude: first.Longitude,
            Value: value,
            UnitId: ctx.CanonicalUnit.Id,
            TotalPointsCount: totalCount,
            ValidPointsCount: validCount);
    }

    // ─── Raw fallback path (p95) ────────────────────────────────────────────────

    private async Task<IReadOnlyList<TimeSeriesPoint>> GetTimeSeriesFromRawAsync(
        Guid pollutantId, Guid emissionSourceId, DateTime from, DateTime to,
        BucketWindow window, CancellationToken cancellationToken)
    {
        var bucketLiteral = BucketLiteral(window);
        var tenantClause = BuildTenantClause();

        var sql = $@"
            SELECT
                time_bucket(INTERVAL '{bucketLiteral}', rm.time) AS bucket_start,
                (percentile_cont(0.95) WITHIN GROUP (ORDER BY rm.raw_value))::numeric(18,6) AS value,
                COUNT(*)::int AS total_count,
                COUNT(*) FILTER (WHERE rm.quality = 0)::int AS valid_count
            FROM raw_measurement rm
            JOIN emission_source es ON es.id = rm.emission_source_id
            WHERE rm.pollutant_id = @pollutant_id
              AND rm.emission_source_id = @emission_source_id
              AND rm.time >= @from
              AND rm.time < @to
              AND es.deleted_at IS NULL
              {tenantClause}
            GROUP BY bucket_start
            ORDER BY bucket_start";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "emission_source_id", NpgsqlDbType.Uuid, emissionSourceId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);

        return await ReadTimeSeriesAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<HeatmapPoint>> GetHeatmapFromRawAsync(
        Guid pollutantId, DateTime from, DateTime to, BoundingBox? bbox, CancellationToken cancellationToken)
    {
        var tenantClause = BuildTenantClause();
        var bboxClause = bbox is null
            ? string.Empty
            : "AND ST_Intersects(es.location, ST_MakeEnvelope(@min_lng, @min_lat, @max_lng, @max_lat, 4326))";

        var sql = $@"
            SELECT
                es.id AS emission_source_id,
                ST_Y(es.location) AS latitude,
                ST_X(es.location) AS longitude,
                (percentile_cont(0.95) WITHIN GROUP (ORDER BY rm.raw_value))::numeric(18,6) AS value,
                (array_agg(rm.unit_id))[1] AS unit_id,
                COUNT(*)::int AS total_count,
                COUNT(*) FILTER (WHERE rm.quality = 0)::int AS valid_count
            FROM raw_measurement rm
            JOIN emission_source es ON es.id = rm.emission_source_id
            WHERE rm.pollutant_id = @pollutant_id
              AND rm.time >= @from
              AND rm.time < @to
              AND es.deleted_at IS NULL
              {tenantClause}
              {bboxClause}
            GROUP BY es.id, es.location";

        await using var command = await CreateCommandAsync(sql, cancellationToken);
        AddParam(command, "pollutant_id", NpgsqlDbType.Uuid, pollutantId);
        AddParam(command, "from", NpgsqlDbType.TimestampTz, EnsureUtc(from));
        AddParam(command, "to", NpgsqlDbType.TimestampTz, EnsureUtc(to));
        AddTenantParam(command);
        AddBboxParams(command, bbox);

        return await ReadHeatmapAsync(command, cancellationToken);
    }

    private static void AddBboxParams(NpgsqlCommand command, BoundingBox? bbox)
    {
        if (bbox is null) return;
        AddParam(command, "min_lng", NpgsqlDbType.Double, bbox.MinLongitude);
        AddParam(command, "min_lat", NpgsqlDbType.Double, bbox.MinLatitude);
        AddParam(command, "max_lng", NpgsqlDbType.Double, bbox.MaxLongitude);
        AddParam(command, "max_lat", NpgsqlDbType.Double, bbox.MaxLatitude);
    }

    // ─── Result readers ────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<TimeSeriesPoint>> ReadTimeSeriesAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var results = new List<TimeSeriesPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TimeSeriesPoint(
                BucketStart: DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                Value: reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                TotalPointsCount: reader.GetInt32(2),
                ValidPointsCount: reader.GetInt32(3)));
        }
        return results;
    }

    private static async Task<IReadOnlyList<HeatmapPoint>> ReadHeatmapAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var results = new List<HeatmapPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HeatmapPoint(
                EmissionSourceId: reader.GetGuid(0),
                Latitude: reader.GetDouble(1),
                Longitude: reader.GetDouble(2),
                Value: reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                UnitId: reader.GetGuid(4),
                TotalPointsCount: reader.GetInt32(5),
                ValidPointsCount: reader.GetInt32(6)));
        }
        return results;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        return new NpgsqlCommand(sql, connection);
    }

    private string BuildTenantClause() =>
        context.BypassTenantFilter ? string.Empty : "AND es.enterprise_id = @tenant_id";

    private void AddTenantParam(NpgsqlCommand command)
    {
        if (context.BypassTenantFilter || context.TenantFilterId is null) return;
        AddParam(command, "tenant_id", NpgsqlDbType.Uuid, context.TenantFilterId.Value);
    }

    private static void AddParam(NpgsqlCommand command, string name, NpgsqlDbType type, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.NpgsqlDbType = type;
        p.Value = value;
        command.Parameters.Add(p);
    }

    private static DateTime EnsureUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);

    private static string BucketLiteral(BucketWindow window) => window switch
    {
        BucketWindow.Minute1 => "1 minute",
        BucketWindow.Minute5 => "5 minutes",
        BucketWindow.Minute10 => "10 minutes",
        BucketWindow.Minute15 => "15 minutes",
        BucketWindow.Minute30 => "30 minutes",
        BucketWindow.HalfHour => "30 minutes",
        BucketWindow.Hour1 => "1 hour",
        BucketWindow.Hour6 => "6 hours",
        BucketWindow.Hour24 => "24 hours",
        BucketWindow.Day1 => "1 day",
        BucketWindow.Month1 => "1 month",
        BucketWindow.Year1 => "1 year",
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, null)
    };

}
