using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace RechnungsWerk.Web.Components.Shared;

/// <summary>
/// Prüft das Formularmodell per DataAnnotations inklusive <see cref="IValidatableObject"/> – und auf Wunsch weitere,
/// verschachtelte Modelle (z. B. das Firmenprofil in der Ersteinrichtung), die der Standard-Validator auslässt.
/// </summary>
public sealed class ModelValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? messages;
    private EditContext? subscribedContext;
    private bool validationRequested;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>Weitere Objekte, die zusammen mit dem Formularmodell geprüft werden.</summary>
    [Parameter]
    public IReadOnlyList<object>? Include { get; set; }

    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException($"{nameof(ModelValidator)} muss innerhalb eines EditForm stehen.");
        }

        if (ReferenceEquals(subscribedContext, CurrentEditContext))
        {
            return;
        }

        Unsubscribe();
        subscribedContext = CurrentEditContext;
        messages = new ValidationMessageStore(subscribedContext);
        subscribedContext.OnValidationRequested += OnValidationRequested;
        subscribedContext.OnFieldChanged += OnFieldChanged;
    }

    public void Dispose() => Unsubscribe();

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        validationRequested = true;
        ValidateAll();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        // Erst nach dem ersten Absenden live nachprüfen – vorher stören rote Hinweise beim Ausfüllen.
        if (validationRequested)
        {
            ValidateAll();
        }
    }

    private void ValidateAll()
    {
        if (subscribedContext is null || messages is null)
        {
            return;
        }

        messages.Clear();
        foreach (var model in (IEnumerable<object>)[subscribedContext.Model, .. Include ?? []])
        {
            foreach (var result in Validate(model))
            {
                var members = result.MemberNames.Any() ? result.MemberNames : [string.Empty];
                foreach (var member in members)
                {
                    messages.Add(new FieldIdentifier(model, member), result.ErrorMessage ?? "Ungültige Eingabe.");
                }
            }
        }

        subscribedContext.NotifyValidationStateChanged();
    }

    /// <summary>Alle Fehler eines Modells auf einmal: DataAnnotations und <see cref="IValidatableObject"/>.</summary>
    internal static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        if (!Validator.TryValidateObject(model, context, results, validateAllProperties: true) && model is IValidatableObject validatable)
        {
            // TryValidateObject prüft IValidatableObject erst, wenn alle Feldregeln bestehen – Fehler erschienen sonst stufenweise.
            foreach (var extra in validatable.Validate(context))
            {
                if (!results.Any(r => r.ErrorMessage == extra.ErrorMessage && r.MemberNames.SequenceEqual(extra.MemberNames)))
                {
                    results.Add(extra);
                }
            }
        }

        return results;
    }

    private void Unsubscribe()
    {
        if (subscribedContext is not null)
        {
            subscribedContext.OnValidationRequested -= OnValidationRequested;
            subscribedContext.OnFieldChanged -= OnFieldChanged;
            messages?.Clear();
            subscribedContext = null;
        }
    }
}
