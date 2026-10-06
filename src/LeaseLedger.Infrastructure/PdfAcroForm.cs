using iTextSharp.text.pdf;
using LeaseLedger.Application;
using LeaseLedger.Domain;

namespace LeaseLedger.Infrastructure;

public sealed record ScannedPdfField(string Name, string Label, PdfFieldKind Kind, IReadOnlyList<string> Options);

public sealed record PdfFillAssignment(string Name, PdfFieldKind Kind, string Value, IReadOnlyList<string> Options);

public static class PdfAcroForm
{
    public static IReadOnlyList<ScannedPdfField> Scan(byte[] pdf)
    {
        using var reader = Open(pdf);
        var form = reader.AcroFields;
        var scanned = new List<ScannedPdfField>();
        if (form?.Fields is not null)
        {
            foreach (var key in form.Fields.Keys)
            {
                if (key is not string name || name.Length == 0)
                    continue;

                var kind = MapKind(form.GetFieldType(name));
                if (kind is null)
                    continue;

                scanned.Add(new ScannedPdfField(name, PdfFieldCatalog.ShortLabel(name), kind.Value, States(form, name)));
            }
        }

        if (scanned.Count == 0)
        {
            throw new LeaseLedgerException(form?.Xfa is { XfaPresent: true }
                ? "This PDF uses an XFA form, which cannot be filled here. Upload a PDF with standard AcroForm fields."
                : "This PDF has no fillable form fields. Upload a PDF with AcroForm fields. Scanned pages are not supported.");
        }

        return scanned
            .OrderBy(field => field.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(field => field.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static byte[] Fill(byte[] pdf, IReadOnlyList<PdfFillAssignment> assignments)
    {
        var reader = Open(pdf);
        var output = new MemoryStream();
        var stamper = new PdfStamper(reader, output);
        try
        {
            var form = stamper.AcroFields;
            form.GenerateAppearances = true;
            foreach (var assignment in assignments)
                Apply(form, assignment);

            stamper.FormFlattening = false;
            stamper.Writer.CloseStream = false;
            stamper.Close();
            return output.ToArray();
        }
        finally
        {
            reader.Close();
            output.Dispose();
        }
    }

    public static string ReadField(byte[] pdf, string name)
    {
        using var reader = Open(pdf);
        return reader.AcroFields.GetField(name) ?? "";
    }

    private static void Apply(AcroFields form, PdfFillAssignment assignment)
    {
        if (assignment.Kind == PdfFieldKind.Signature || string.IsNullOrWhiteSpace(assignment.Name))
            return;

        var states = States(form, assignment.Name);
        if (states.Count == 0)
            states = assignment.Options.ToList();

        switch (assignment.Kind)
        {
            case PdfFieldKind.Checkbox:
                ApplyCheckbox(form, assignment.Name, assignment.Value, states);
                break;
            case PdfFieldKind.Radio:
            case PdfFieldKind.Choice:
                ApplyChoice(form, assignment.Name, assignment.Value, states);
                break;
            default:
                if (!string.IsNullOrEmpty(assignment.Value))
                    form.SetField(assignment.Name, assignment.Value);
                break;
        }
    }

    private static void ApplyCheckbox(AcroFields form, string name, string value, IReadOnlyList<string> states)
    {
        var trimmed = value.Trim();
        var onStates = states.Where(state => !IsOff(state)).ToArray();
        if (trimmed.Length == 0 || IsOff(trimmed))
        {
            var off = states.FirstOrDefault(IsOff);
            if (off is not null)
                form.SetField(name, off);
            return;
        }

        var match = onStates.FirstOrDefault(state => state.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null && IsCheckedWord(trimmed))
            match = onStates.FirstOrDefault();
        if (match is not null)
            form.SetField(name, match);
    }

    private static void ApplyChoice(AcroFields form, string name, string value, IReadOnlyList<string> states)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var match = states.FirstOrDefault(state => state.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) && !IsOff(state));
        if (match is null && states.Count == 0)
            match = value.Trim();
        if (match is not null)
            form.SetField(name, match);
    }

    private static List<string> States(AcroFields form, string name)
    {
        var states = form.GetAppearanceStates(name);
        if (states is null || states.Length == 0)
            return [];

        return states.Where(state => !string.IsNullOrWhiteSpace(state)).Distinct(StringComparer.Ordinal).ToList();
    }

    private static PdfFieldKind? MapKind(int fieldType) => fieldType switch
    {
        AcroFields.FIELD_TYPE_CHECKBOX => PdfFieldKind.Checkbox,
        AcroFields.FIELD_TYPE_RADIOBUTTON => PdfFieldKind.Radio,
        AcroFields.FIELD_TYPE_TEXT => PdfFieldKind.Text,
        AcroFields.FIELD_TYPE_LIST or AcroFields.FIELD_TYPE_COMBO => PdfFieldKind.Choice,
        AcroFields.FIELD_TYPE_SIGNATURE => PdfFieldKind.Signature,
        _ => null
    };

    private static bool IsOff(string state) =>
        state.Equals("Off", StringComparison.OrdinalIgnoreCase)
        || state.Equals("No", StringComparison.OrdinalIgnoreCase)
        || state.Equals("False", StringComparison.OrdinalIgnoreCase)
        || state.Equals("0", StringComparison.Ordinal);

    private static bool IsCheckedWord(string value) =>
        value.Equals("Yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("True", StringComparison.OrdinalIgnoreCase)
        || value.Equals("On", StringComparison.OrdinalIgnoreCase)
        || value.Equals("1", StringComparison.Ordinal)
        || value.Equals("Checked", StringComparison.OrdinalIgnoreCase);

    private static PdfReader Open(byte[] pdf)
    {
        if (pdf is null || pdf.Length == 0 || !LooksLikePdf(pdf))
            throw new LeaseLedgerException("That file is not a PDF.");

        try
        {
            return new PdfReader(pdf);
        }
        catch (Exception exception) when (exception is iTextSharp.text.exceptions.BadPasswordException or iTextSharp.text.pdf.BadPasswordException)
        {
            throw new LeaseLedgerException("This PDF is password-protected. Remove the password and upload it again.");
        }
        catch (iTextSharp.text.exceptions.InvalidPdfException)
        {
            throw new LeaseLedgerException("That file is not a readable PDF.");
        }
        catch (IOException)
        {
            throw new LeaseLedgerException("That file is not a readable PDF.");
        }
    }

    private static bool LooksLikePdf(byte[] content)
    {
        var length = Math.Min(content.Length, 1024);
        for (var index = 0; index < length - 4; index++)
        {
            if (content[index] == (byte)'%'
                && content[index + 1] == (byte)'P'
                && content[index + 2] == (byte)'D'
                && content[index + 3] == (byte)'F')
                return true;
        }

        return false;
    }
}
