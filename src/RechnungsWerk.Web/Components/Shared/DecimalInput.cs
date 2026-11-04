using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using RechnungsWerk.Core.Formatting;

namespace RechnungsWerk.Web.Components.Shared;

/// <summary>
/// Zahlenfeld in deutscher Schreibweise: Komma als Dezimaltrenner, Punkt nur als Tausendertrenner. Ein Feld vom Typ
/// „number“ meldet für „1.500“ den Wert 1,5 an den Server – dort wäre die Eingabe nicht mehr zu unterscheiden.
/// </summary>
public sealed class DecimalInput : InputBase<decimal>
{
    [DisallowNull]
    public ElementReference? Element { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "type", "text");
        builder.AddAttribute(3, "inputmode", "decimal");
        builder.AddAttribute(4, "autocomplete", "off");
        if (!string.IsNullOrEmpty(NameAttributeValue))
        {
            builder.AddAttribute(5, "name", NameAttributeValue);
        }

        if (!string.IsNullOrEmpty(CssClass))
        {
            builder.AddAttribute(6, "class", CssClass);
        }

        builder.AddAttribute(7, "value", CurrentValueAsString);
        builder.AddAttribute(8, "onchange", EventCallback.Factory.CreateBinder<string?>(this, value => CurrentValueAsString = value, CurrentValueAsString));
        builder.SetUpdatesAttributeName("value");
        builder.AddElementReferenceCapture(9, reference => Element = reference);
        builder.CloseElement();
    }

    // Ohne feste Zahl von Nachkommastellen, damit nichts gerundet angezeigt wird, was so nicht gespeichert ist.
    protected override string FormatValueAsString(decimal value) => value.ToString("#,##0.############", GermanFormat.Culture);

    protected override bool TryParseValueFromString(string? value, out decimal result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (GermanFormat.TryParseDecimal(value, out result))
        {
            validationErrorMessage = null;
            return true;
        }

        var message = string.IsNullOrWhiteSpace(value) ? "Bitte gib eine Zahl ein." : "Bitte gib die Zahl mit Komma ein, z. B. 1.234,50.";
        validationErrorMessage = DisplayName is null ? message : $"{DisplayName}: {message}";
        return false;
    }
}
