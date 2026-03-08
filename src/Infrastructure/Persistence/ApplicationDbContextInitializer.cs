using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Infrastructure.Persistence;

public class ApplicationDbContextInitializer(
    ILogger<ApplicationDbContextInitializer> logger,
    ApplicationDbContext dbContext)
{
    public async Task InitialiseAsync()
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            await SeedDataAsync();
            await SeedDomainEntitiesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An error occurred while initialising the database.");
            throw;
        }
    }

    private async Task SeedDataAsync()
    {
        await SeedSectorsAsync();
        await SeedIedCategoriesAsync();
        await SeedMeasureUnitsAsync();
        await SeedPollutantsAsync();
    }


    private async Task SeedSectorsAsync()
    {
        if (await dbContext.Set<Sector>().AnyAsync())
        {
            logger.LogInformation("Skipping seeding: Sectors (KVED Activities) already exist.");
            return;
        }


        var sectors = new List<Sector>
        {
            // Добувна промисловість (Секція B)
            Sector.New(Guid.NewGuid(), "Добування кам'яного вугілля", "05.10"),
            Sector.New(Guid.NewGuid(), "Добування бурого вугілля (лігніту)", "05.20"),
            Sector.New(Guid.NewGuid(), "Добування залізних руд", "07.10"),

            // Переробна промисловість (Секція C)
            Sector.New(Guid.NewGuid(), "Виробництво м'яса", "10.11"),
            Sector.New(Guid.NewGuid(), "Перероблення молока, виробництво масла та сиру", "10.51"),
            Sector.New(Guid.NewGuid(), "Виробництво паперової маси", "17.11"),
            Sector.New(Guid.NewGuid(), "Виробництво паперу та картону", "17.12"),
            Sector.New(Guid.NewGuid(), "Виробництво коксу та коксопродуктів", "19.10"),
            Sector.New(Guid.NewGuid(), "Виробництво продуктів нафтоперероблення", "19.20"),
            Sector.New(Guid.NewGuid(), "Виробництво інших основних неорганічних хімічних речовин", "20.13"),
            Sector.New(Guid.NewGuid(), "Виробництво інших основних органічних хімічних речовин", "20.14"),
            Sector.New(Guid.NewGuid(), "Виробництво добрив і азотних сполук", "20.15"),
            Sector.New(Guid.NewGuid(), "Виробництво фармацевтичних препаратів і матеріалів", "21.20"),
            Sector.New(Guid.NewGuid(), "Виробництво листового скла", "23.11"),
            Sector.New(Guid.NewGuid(), "Виробництво цементу", "23.51"),
            Sector.New(Guid.NewGuid(), "Виробництво вапна та гіпсових сумішей", "23.52"),
            Sector.New(Guid.NewGuid(), "Виробництво чавуну, сталі та феросплавів", "24.10"),
            Sector.New(Guid.NewGuid(), "Виробництво алюмінію", "24.42"),
            Sector.New(Guid.NewGuid(), "Виробництво свинцю, цинку й олова", "24.43"),
            Sector.New(Guid.NewGuid(), "Виробництво міді", "24.44"),

            // Постачання електроенергії (Секція D)
            Sector.New(Guid.NewGuid(), "Виробництво електроенергії", "35.11"),
            Sector.New(Guid.NewGuid(), "Постачання пари, гарячої води та кондиційованого повітря", "35.30"),

            // Водопостачання, поводження з відходами (Секція E)
            Sector.New(Guid.NewGuid(), "Збирання небезпечних відходів", "38.12"),
            Sector.New(Guid.NewGuid(), "Оброблення та видалення небезпечних відходів", "38.22"),
            Sector.New(Guid.NewGuid(), "Оброблення та видалення безпечних відходів", "38.21"),
            Sector.New(Guid.NewGuid(), "Очищення стічних вод", "37.00"), // (Секція E)

            // Сільське господарство (Секція A)
            Sector.New(Guid.NewGuid(), "Розведення свиней", "01.46"),
            Sector.New(Guid.NewGuid(), "Розведення свійської птиці", "01.47")
        };

        await dbContext.Set<Sector>().AddRangeAsync(sectors);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedIedCategoriesAsync()
    {
        if (await dbContext.Set<IedCategory>().AnyAsync())
        {
            logger.LogInformation("Skipping seeding: IedCategories already exist.");
            return;
        }


        var categories = new List<IedCategory>
        {
            // --- 1. Енергетика ---
            IedCategory.New(Guid.NewGuid(), "1.1",
                "Спалювання палива в установках з номінальною тепловою потужністю 50 МВт або більше"),
            IedCategory.New(Guid.NewGuid(), "1.2", "Виплавка або рафінування нафти і газу"),
            IedCategory.New(Guid.NewGuid(), "1.3", "Виробництво коксу"),
            IedCategory.New(Guid.NewGuid(), "1.4.a", "Газифікація або зрідження вугілля"),
            IedCategory.New(Guid.NewGuid(), "1.4.b",
                "Газифікація або зрідження іншого палива в установках номінальною тепловою потужністю 20 МВт або більше"),

            // --- 2. Виробництво та обробка металів ---
            IedCategory.New(Guid.NewGuid(), "2.1",
                "Випалювання або спікання металевої руди (включаючи сульфідну руду)"),
            IedCategory.New(Guid.NewGuid(), "2.2",
                "Виробництво чавуну або сталі (первинна або вторинна плавка), з потужністю, що перевищує 2.5 тонни на годину"),
            IedCategory.New(Guid.NewGuid(), "2.3.a",
                "Гаряча прокатка чорних металів (з потужністю, що перевищує 20 тонн сирої сталі на годину)"),
            IedCategory.New(Guid.NewGuid(), "2.3.b",
                "Ковальство із застосуванням молотів, енергія яких перевищує 50 кДж на молот"),
            IedCategory.New(Guid.NewGuid(), "2.4",
                "Виробництво кольорових металів з руди, концентратів або вторинної сировини"),
            IedCategory.New(Guid.NewGuid(), "2.5.a",
                "Обробка кольорових металів: плавлення, легування, рафінування"),
            IedCategory.New(Guid.NewGuid(), "2.6",
                "Обробка поверхні металів або пластмас з використанням електролітичних/хімічних процесів (об'єм ванни > 30 м³)"),

            // --- 3. Мінеральна промисловість ---
            IedCategory.New(Guid.NewGuid(), "3.1.a",
                "Виробництво цементного клінкеру в обертових випалювальних печах (> 500 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "3.1.b", "Виробництво вапна у випалювальних печах (> 50 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "3.3", "Виробництво скла, включаючи скловолокно (> 20 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "3.4",
                "Плавлення мінеральних речовин, включаючи виробництво мінеральної вати (> 20 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "3.5",
                "Виробництво керамічних виробів шляхом випалювання (> 75 тонн/добу)"),

            // --- 4. Хімічна промисловість ---
            IedCategory.New(Guid.NewGuid(), "4.1.a", "Виробництво органічних хімікатів: прості вуглеводні"),
            IedCategory.New(Guid.NewGuid(), "4.1.b",
                "Виробництво органічних хімікатів: кисневмісні вуглеводні (спирти, альдегіди тощо)"),
            IedCategory.New(Guid.NewGuid(), "4.1.c", "Виробництво органічних хімікатів: сірковмісні вуглеводні"),
            IedCategory.New(Guid.NewGuid(), "4.2.a",
                "Виробництво неорганічних хімікатів: гази (аміак, хлор тощо)"),
            IedCategory.New(Guid.NewGuid(), "4.2.b",
                "Виробництво неорганічних хімікатів: кислоти (сірчана, азотна тощо)"),
            IedCategory.New(Guid.NewGuid(), "4.3", "Виробництво фосфорних, азотних або калійних добрив"),
            IedCategory.New(Guid.NewGuid(), "4.4", "Виробництво засобів захисту рослин або біоцидів"),
            IedCategory.New(Guid.NewGuid(), "4.5", "Виробництво фармацевтичної продукції"),

            // --- 5. Поводження з відходами ---
            IedCategory.New(Guid.NewGuid(), "5.1",
                "Видалення або утилізація небезпечних відходів (> 10 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "5.2.a", "Спалювання не-небезпечних відходів (> 3 тонн/годину)"),
            IedCategory.New(Guid.NewGuid(), "5.3.a", "Захоронення небезпечних відходів (> 10 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "5.3.b",
                "Захоронення не-небезпечних відходів (> 10 тонн/добу або загальна > 25 000 тонн)"),
            IedCategory.New(Guid.NewGuid(), "5.5", "Тимчасове зберігання небезпечних відходів (> 50 тонн)"),

            // --- 6. Інші види діяльності ---
            IedCategory.New(Guid.NewGuid(), "6.1.a",
                "Виробництво целюлози з деревини або подібних волокнистих матеріалів"),
            IedCategory.New(Guid.NewGuid(), "6.1.b", "Виробництво паперу або картону (> 20 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "6.3", "Обробка та просочування деревини (> 75 м³/добу)"),
            IedCategory.New(Guid.NewGuid(), "6.4.b",
                "Обробка харчових продуктів з тваринної сировини (> 75 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "6.4.c", "Обробка молока (> 200 тонн/добу, середньорічне значення)"),
            IedCategory.New(Guid.NewGuid(), "6.5", "Утилізація або переробка туш тварин (> 10 тонн/добу)"),
            IedCategory.New(Guid.NewGuid(), "6.6.a", "Інтенсивне вирощування свійської птиці (> 40 000 місць)"),
            IedCategory.New(Guid.NewGuid(), "6.6.b",
                "Інтенсивне вирощування свиней (> 2 000 місць для свиней понад 30 кг)"),
            IedCategory.New(Guid.NewGuid(), "6.6.c", "Інтенсивне вирощування свиней (> 750 місць для свиноматок)"),
            IedCategory.New(Guid.NewGuid(), "6.7",
                "Обробка поверхні речовин з використанням органічних розчинників (> 150 кг/год або > 200 тонн/рік)"),
            IedCategory.New(Guid.NewGuid(), "6.9",
                "Вилов та зберігання CO2 з установок, що підпадають під цю Директиву"),
            IedCategory.New(Guid.NewGuid(), "6.11",
                "Очищення стічних вод з установок, що підпадають під цю Директиву")
        };

        await dbContext.Set<IedCategory>().AddRangeAsync(categories);
        await dbContext.SaveChangesAsync();
    }

    private List<Site> GenerateSitesForEnterprise(
        Guid enterpriseId,
        string enterpriseName,
        int count = 2)
    {
        var sites = new List<Site>();

        for (int i = 1; i <= count; i++)
        {
            sites.Add(Site.New(
                Guid.NewGuid(),
                $"{enterpriseName} — майданчик {i}",
                $"Адреса майданчика {i} підприємства «{enterpriseName}»",
                1000,
                enterpriseId
            ));
        }

        return sites;
    }

    private List<Installation> GenerateInstallationsForSite(
        Guid siteId,
        string siteName,
        Guid categoryId,
        int count = 3)
    {
        var installations = new List<Installation>();

        for (int i = 1; i <= count; i++)
        {
            installations.Add(Installation.New(
                Guid.NewGuid(),
                $"{siteName} — установка {i}",
                categoryId,
                siteId,
                InstallationStatus.Operating
            ));
        }

        return installations;
    }

    private List<EmissionSource> GenerateEmissionSourcesForInstallation(
        Guid installationId,
        string installationName,
        int count = 5)
    {
        var sources = new List<EmissionSource>();
        var rnd = new Random();

        for (int i = 1; i <= count; i++)
        {
            bool isAir = rnd.NextDouble() > 0.2;

            string code = isAir
                ? $"SRC_A_{i}"
                : $"SRC_W_{i}";

            if (isAir)
            {
                sources.Add(AirEmissionSource.New(
                    Guid.NewGuid(),
                    installationId,
                    code,
                    height: rnd.Next(30, 250),
                    diameter: rnd.Next(2, 10),
                    designFlowRate: rnd.Next(100000, 5000000)
                ));
            }
            else
            {
                sources.Add(WaterEmissionSource.New(
                    Guid.NewGuid(),
                    installationId,
                    code,
                    receiver: "Dnipro",
                    designFlowRate: rnd.Next(20000, 100000)
                ));
            }
        }

        return sources;
    }

    private List<MonitoringDevice> GenerateMonitoringDevicesForInstallation(
        Guid installationId,
        List<EmissionSource> emissionSources,
        int count = 4)
    {
        var devices = new List<MonitoringDevice>();
        var rnd = new Random();

        for (int i = 1; i <= count; i++)
        {
            // 50% шанс що пристрій прив’язаний до джерела
            Guid? sourceId = null;

            if (rnd.NextDouble() > 0.5 && emissionSources.Count > 0)
            {
                var picked = emissionSources[rnd.Next(emissionSources.Count)];
                sourceId = picked.Id;
            }

            var deviceType = (MonitoringDeviceType)rnd.Next(0, 3);

            var device = MonitoringDevice.New(
                id: Guid.NewGuid(),
                installationId: installationId,
                emissionSourceId: sourceId,
                model: $"Model-{deviceType}-{i}",
                serialNumber: $"SN-{Guid.NewGuid().ToString()[..8]}",
                type: deviceType,
                isOnline: rnd.NextDouble() > 0.3,
                notes: sourceId is null
                    ? "Device installed at installation level"
                    : "Device installed on emission source"
            );

            devices.Add(device);
        }

        return devices;
    }


    private async Task SeedPollutantsAsync()
    {
        if (await dbContext.Set<Pollutant>().AnyAsync())
            return;

        var pollutants = new List<Pollutant>
        {
            Pollutant.New(Guid.NewGuid(), "CO", "Чадний газ"),
            Pollutant.New(Guid.NewGuid(), "CO₂", "Вуглекислий газ"),
            Pollutant.New(Guid.NewGuid(), "NO", "Оксид азоту"),
            Pollutant.New(Guid.NewGuid(), "NO₂", "Діоксид азоту"),
            Pollutant.New(Guid.NewGuid(), "SO₂", "Діоксид сірки"),
            Pollutant.New(Guid.NewGuid(), "H₂S", "Сірководень"),
            Pollutant.New(Guid.NewGuid(), "PM10", "Тверді частинки PM10"),
            Pollutant.New(Guid.NewGuid(), "PM2.5", "Тверді частинки PM2.5"),
            Pollutant.New(Guid.NewGuid(), "NH₃", "Аміак"),
            Pollutant.New(Guid.NewGuid(), "CH₄", "Метан")
        };

        await dbContext.Set<Pollutant>().AddRangeAsync(pollutants);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded Pollutants.");
    }

    private async Task SeedMeasureUnitsAsync()
    {
        if (await dbContext.Set<MeasureUnit>().AnyAsync())
            return;

        var units = new List<MeasureUnit>
        {
            // Mass concentration
            MeasureUnit.New(Guid.NewGuid(), "mg/m³", MeasureUnitDimension.MassConcentration, 1m),
            MeasureUnit.New(Guid.NewGuid(), "g/m³", MeasureUnitDimension.MassConcentration, 1000m),

            // Mass flow
            MeasureUnit.New(Guid.NewGuid(), "kg/h", MeasureUnitDimension.MassFlow, 1m),
            MeasureUnit.New(Guid.NewGuid(), "t/yr", MeasureUnitDimension.MassFlow,
                876000m), // 1 t/yr = 1000kg / (365*24)

            // Volumetric flow
            MeasureUnit.New(Guid.NewGuid(), "m³/h", MeasureUnitDimension.VolumetricFlow, 1m),

            // Dimensionless
            MeasureUnit.New(Guid.NewGuid(), "ppm", MeasureUnitDimension.Dimensionless, 1m),
            MeasureUnit.New(Guid.NewGuid(), "%", MeasureUnitDimension.Dimensionless, 10000m)
        };

        await dbContext.Set<MeasureUnit>().AddRangeAsync(units);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded MeasureUnits.");
    }

    private async Task SeedDomainEntitiesAsync()
    {
        if (await dbContext.Set<Enterprise>().AnyAsync())
            return;

        var sectorMetal = await dbContext.Set<Sector>().FirstAsync(s => s.Code == "24.10");
        var sectorPower = await dbContext.Set<Sector>().FirstAsync(s => s.Code == "35.11");
        var sectorCement = await dbContext.Set<Sector>().FirstAsync(s => s.Code == "23.51");
        var sectorCoke = await dbContext.Set<Sector>().FirstAsync(s => s.Code == "19.10");
        var sectorWater = await dbContext.Set<Sector>().FirstAsync(s => s.Code == "37.00");

        var iedMetal = await dbContext.Set<IedCategory>().FirstAsync(i => i.Code == "2.2");
        var iedPower = await dbContext.Set<IedCategory>().FirstAsync(i => i.Code == "1.1");
        var iedCement = await dbContext.Set<IedCategory>().FirstAsync(i => i.Code == "3.1.a");
        var iedCoke = await dbContext.Set<IedCategory>().FirstAsync(i => i.Code == "1.3");
        var iedWater = await dbContext.Set<IedCategory>().FirstAsync(i => i.Code == "6.11");

        // ENTERPRISES
        var enterprises = new List<Enterprise>
        {
            Enterprise.New(Guid.NewGuid(), "ТОВ «Метінвест Холдинг»", "30101010",
                "м. Маріуполь, вул. Набережна, 1", RiskGroup.High, sectorMetal.Id),

            Enterprise.New(Guid.NewGuid(), "ДТЕК Енерго", "30202020",
                "м. Київ, вул. Велика Васильківська, 5", RiskGroup.High, sectorPower.Id),

            Enterprise.New(Guid.NewGuid(), "ПРАТ «Кривий Ріг Цемент»", "30303030",
                "м. Кривий Ріг, вул. Заводська, 10", RiskGroup.Average, sectorCement.Id),

            Enterprise.New(Guid.NewGuid(), "ПрАТ «Авдіївський КХЗ»", "30404040",
                "м. Авдіївка, вул. Індустріальна, 1", RiskGroup.High, sectorCoke.Id),

            Enterprise.New(Guid.NewGuid(), "ПрАТ «АК Київводоканал»", "30505050",
                "м. Київ, вул. Лейпцизька, 1А", RiskGroup.Average, sectorWater.Id)
        };

        await dbContext.Set<Enterprise>().AddRangeAsync(enterprises);

        // SITES + INSTALLATIONS + EMISSION SOURCES
        var allSites = new List<Site>();
        var allInstallations = new List<Installation>();
        var allEmissionSources = new List<EmissionSource>();
        var allDevices = new List<MonitoringDevice>();

        var random = new Random();

        foreach (var enterprise in enterprises)
        {
            // 2 SITES
            var generatedSites = GenerateSitesForEnterprise(
                enterprise.Id,
                enterprise.Name,
                count: 2
            );

            allSites.AddRange(generatedSites);

            foreach (var site in generatedSites)
            {
                // Random IED category
                var cat = new[]
                {
                    iedMetal.Id, iedPower.Id, iedCement.Id, iedCoke.Id, iedWater.Id
                }[random.Next(0, 5)];

                // 3 INSTALLATIONS
                var generatedInst = GenerateInstallationsForSite(
                    site.Id,
                    site.Name,
                    cat,
                    count: 3
                );

                allInstallations.AddRange(generatedInst);

                foreach (var inst in generatedInst)
                {
                    var sources = GenerateEmissionSourcesForInstallation(
                        inst.Id,
                        inst.Name,
                        count: 5
                    );

                    allEmissionSources.AddRange(sources);

                    // GENERATE 4 DEVICES PER INSTALLATION
                    var devices = GenerateMonitoringDevicesForInstallation(
                        inst.Id,
                        sources,
                        count: 4
                    );

                    allDevices.AddRange(devices);
                }
            }
        }

        await dbContext.Set<Site>().AddRangeAsync(allSites);
        await dbContext.Set<Installation>().AddRangeAsync(allInstallations);
        await dbContext.Set<EmissionSource>().AddRangeAsync(allEmissionSources);
        await dbContext.Set<MonitoringDevice>().AddRangeAsync(allDevices);


        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Seeded Enterprises → Sites (2 each) → Installations (3 each) → EmissionSources (5 each) -> MonitoringDevices (4 each).");
    }
}