using iTextSharp.text;
using iTextSharp.text.pdf;
using LeaseLedger.Application;
using LeaseLedger.Domain;
using LeaseLedger.Infrastructure;
using PdfForms = LeaseLedger.Infrastructure.PdfAcroForm;

namespace LeaseLedger.Domain.Tests;

public class PdfAcroFormTests
{
    [Fact]
    public void Fills_a_text_field_without_flattening_the_form()
    {
        var pdf = Form();
        var scanned = PdfForms.Scan(pdf);

        Assert.Contains(scanned, field => field.Name == "TenantName" && field.Kind == PdfFieldKind.Text);
        Assert.Contains(scanned, field => field.Name == "Agree" && field.Kind == PdfFieldKind.Checkbox);

        var filled = PdfForms.Fill(pdf,
        [
            new PdfFillAssignment("TenantName", PdfFieldKind.Text, "Ada Lovelace", []),
            new PdfFillAssignment("Agree", PdfFieldKind.Checkbox, "Yes", ["Off", "Yes"])
        ]);

        Assert.Equal("Ada Lovelace", PdfForms.ReadField(filled, "TenantName"));
        Assert.Equal("Yes", PdfForms.ReadField(filled, "Agree"));
        Assert.Contains(PdfForms.Scan(filled), field => field.Name == "TenantName");
    }

    [Fact]
    public void Leaves_a_checkbox_unchecked_when_the_value_does_not_match()
    {
        var filled = PdfForms.Fill(Form(),
        [
            new PdfFillAssignment("Agree", PdfFieldKind.Checkbox, "Ada Lovelace", [])
        ]);

        Assert.NotEqual("Yes", PdfForms.ReadField(filled, "Agree"));
    }

    [Fact]
    public void Rejects_a_pdf_without_form_fields()
    {
        var error = Assert.Throws<LeaseLedgerException>(() => PdfForms.Scan(Flat()));
        Assert.Contains("no fillable form fields", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Form()
    {
        using var stream = new MemoryStream();
        var document = new Document();
        var writer = PdfWriter.GetInstance(document, stream);
        document.Open();
        document.Add(new Paragraph("Sample"));
        writer.AddAnnotation(new TextField(writer, new Rectangle(36, 700, 320, 730), "TenantName").GetTextField());
        var checkbox = new RadioCheckField(writer, new Rectangle(36, 660, 56, 680), "Agree", "Yes")
        {
            CheckType = RadioCheckField.TYPE_CHECK
        };
        writer.AddAnnotation(checkbox.CheckField);
        document.Close();
        return stream.ToArray();
    }

    private static byte[] Flat()
    {
        using var stream = new MemoryStream();
        var document = new Document();
        PdfWriter.GetInstance(document, stream);
        document.Open();
        document.Add(new Paragraph("Flat page"));
        document.Close();
        return stream.ToArray();
    }
}
