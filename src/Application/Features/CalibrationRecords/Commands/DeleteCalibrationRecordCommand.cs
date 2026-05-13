using Application.Features.CalibrationRecords.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.CalibrationRecords.Commands;

public class DeleteCalibrationRecordCommand
    : IRequest<Either<CalibrationRecordException, CalibrationRecord>>,
        IValidatableModel<DeleteCalibrationRecordCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteCalibrationRecordCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteCalibrationRecordCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        return validator;
    }
}
