using System.Text.Json;
using LeaseLedger.Application;
using LeaseLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed partial class LeaseLedgerService
{
    public async Task<IReadOnlyList<PdfTemplateSummaryDto>> GetPdfTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        return await db.PdfFormTemplates.AsNoTracking()
            .Where(template => template.LandlordUserId == user.UserId)
            .OrderBy(template => template.Name)
            .Select(template => new PdfTemplateSummaryDto(
                template.Id,
                template.Name,
                template.FileName,
                template.Fields.Count,
                template.UploadedAt,
                template.NoticeKind))
            .ToListAsync(cancellationToken);
    }

    public async Task<PdfTemplateDetailDto> UploadPdfTemplateAsync(UploadPdfTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var content = request.Content ?? [];
        if (content.Length == 0)
            throw new LeaseLedgerException("Choose a PDF.");
        if (content.Length > PdfFormLimits.MaxBytes)
            throw new LeaseLedgerException("That PDF is larger than 8 MB.");

        var fileName = Path.GetFileName(request.FileName ?? "").Trim();
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new LeaseLedgerException("Upload a .pdf file.");

        var scanned = PdfAcroForm.Scan(content);
        if (scanned.Any(field => field.Name.Length > 1024))
            throw new LeaseLedgerException("A form field name is too long to store.");

        var name = Clean(request.Name) ?? Path.GetFileNameWithoutExtension(fileName);
        if (name.Length > 200)
            throw new LeaseLedgerException("The form name must be 200 characters or less.");

        NoticeKind? noticeKind = null;
        if (request.NoticeKind is NoticeKind requested)
        {
            if (!Enum.IsDefined(requested))
                throw new LeaseLedgerException("That form is not recognized.");
            var taken = await db.PdfFormTemplates.AnyAsync(
                item => item.LandlordUserId == user.UserId && item.NoticeKind == requested,
                cancellationToken);
            if (taken)
                throw new LeaseLedgerException("This form already has a PDF template.");
            noticeKind = requested;
        }

        var template = new PdfFormTemplate
        {
            Id = Guid.NewGuid(),
            LandlordUserId = user.UserId!.Value,
            Name = name,
            FileName = fileName.Length > 260 ? fileName[..260] : fileName,
            Content = content,
            UploadedAt = DateTimeOffset.UtcNow,
            NoticeKind = noticeKind
        };

        var order = 0;
        foreach (var field in scanned)
        {
            template.Fields.Add(new PdfFormField
            {
                Id = Guid.NewGuid(),
                Name = field.Name,
                Label = field.Label.Length > 200 ? field.Label[..200] : field.Label,
                Kind = field.Kind,
                Options = SerializeOptions(field.Options),
                DataKey = field.Kind == PdfFieldKind.Signature ? null : PdfFieldCatalog.Suggest(field.Name),
                Format = PdfValueFormat.Default,
                SortOrder = order++
            });
        }

        db.PdfFormTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToPdfTemplate(template);
    }

    public async Task<PdfTemplateDetailDto?> GetPdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var template = await db.PdfFormTemplates.AsNoTracking()
            .Where(item => item.Id == templateId && item.LandlordUserId == user.UserId)
            .Select(item => new { item.Id, item.Name, item.FileName, item.UploadedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null)
            return null;

        var fields = await db.PdfFormFields.AsNoTracking()
            .Where(field => field.TemplateId == templateId)
            .OrderBy(field => field.SortOrder)
            .ToListAsync(cancellationToken);
        return new PdfTemplateDetailDto(
            template.Id,
            template.Name,
            template.FileName,
            template.UploadedAt,
            fields.Select(ToPdfField).ToList());
    }

    public async Task<PdfFileDto?> GetPdfTemplateFileAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var template = await db.PdfFormTemplates.AsNoTracking()
            .Where(item => item.Id == templateId && item.LandlordUserId == user.UserId)
            .Select(item => new { item.FileName, item.Content })
            .FirstOrDefaultAsync(cancellationToken);
        return template is null ? null : new PdfFileDto(template.FileName, template.Content);
    }

    public async Task<PdfTemplateDetailDto> UpdatePdfTemplateAsync(Guid templateId, UpdatePdfTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var template = await db.PdfFormTemplates
            .Include(item => item.Fields)
            .FirstOrDefaultAsync(item => item.Id == templateId && item.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That form was not found.");

        var name = Required(request.Name, "Enter a name for this form.");
        if (name.Length > 200)
            throw new LeaseLedgerException("The form name must be 200 characters or less.");
        template.Name = name;

        var fields = template.Fields.ToDictionary(field => field.Id);
        foreach (var mapping in request.Fields ?? [])
        {
            if (!fields.TryGetValue(mapping.FieldId, out var field))
                throw new LeaseLedgerException("That form field was not found.");
            if (!Enum.IsDefined(mapping.Format))
                throw new LeaseLedgerException("Choose a format for each field.");

            var dataKey = Clean(mapping.DataKey);
            var constant = Clean(mapping.ConstantValue);
            if (dataKey is not null && constant is not null)
                throw new LeaseLedgerException("Choose a data field or fixed text, not both.");
            if (dataKey is not null && PdfFieldCatalog.Find(dataKey) is null)
                throw new LeaseLedgerException("Choose a value from the tenancy list.");
            if (constant is not null && constant.Length > 500)
                throw new LeaseLedgerException("Fixed text must be 500 characters or less.");
            if (field.Kind == PdfFieldKind.Signature)
            {
                dataKey = null;
                constant = null;
            }

            field.DataKey = dataKey;
            field.ConstantValue = constant;
            field.Format = mapping.Format;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToPdfTemplate(template);
    }

    public async Task DeletePdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var template = await db.PdfFormTemplates
            .FirstOrDefaultAsync(item => item.Id == templateId && item.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That form was not found.");
        db.PdfFormTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PdfFileDto> FillPdfTemplateAsync(Guid templateId, Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var template = await db.PdfFormTemplates.AsNoTracking()
            .Include(item => item.Fields)
            .FirstOrDefaultAsync(item => item.Id == templateId && item.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That form was not found.");

        var tenancy = await VisibleTenancies(user)
            .AsNoTracking()
            .Include(item => item.LedgerEntries)
            .FirstOrDefaultAsync(item => item.Id == tenancyId, cancellationToken)
            ?? throw new LeaseLedgerException("That tenancy was not found.");

        var landlord = await users.FindByIdAsync(tenancy.Unit.Property.LandlordUserId.ToString());
        var context = new PdfFillContext(
            tenancy.TenantName,
            tenancy.TenantEmail,
            tenancy.RentAmount,
            tenancy.RentDueDay,
            tenancy.Frequency,
            tenancy.StartDate,
            tenancy.LastRentIncreaseDate,
            tenancy.Unit.Label,
            tenancy.Unit.Property.Street,
            tenancy.Unit.Property.City,
            tenancy.Unit.Property.Province,
            tenancy.Unit.Property.PostalCode,
            landlord?.DisplayName ?? "",
            landlord?.Email ?? "",
            LedgerMath.Balance(tenancy.LedgerEntries),
            DateOnly.FromDateTime(DateTime.Today));

        var assignments = template.Fields
            .OrderBy(field => field.SortOrder)
            .Select(field => new PdfFillAssignment(
                field.Name,
                field.Kind,
                PdfFieldCatalog.Resolve(context, field.DataKey, field.ConstantValue, field.Format),
                DeserializeOptions(field.Options)))
            .ToList();

        var filled = PdfAcroForm.Fill(template.Content, assignments);
        return new PdfFileDto(DownloadName(template.Name, tenancy.TenantName), filled);
    }

    private static PdfTemplateDetailDto ToPdfTemplate(PdfFormTemplate template) => new(
        template.Id,
        template.Name,
        template.FileName,
        template.UploadedAt,
        template.Fields
            .OrderBy(field => field.SortOrder)
            .Select(ToPdfField)
            .ToList());

    private static PdfFieldDto ToPdfField(PdfFormField field) => new(
        field.Id,
        field.Name,
        field.Label,
        field.Kind,
        DeserializeOptions(field.Options),
        field.DataKey,
        field.ConstantValue,
        field.Format);

    private static string? SerializeOptions(IReadOnlyList<string> options)
    {
        var list = options.Where(option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.Ordinal).ToList();
        while (list.Count > 0)
        {
            var json = JsonSerializer.Serialize(list);
            if (json.Length <= 4000)
                return json;
            list.RemoveAt(list.Count - 1);
        }

        return null;
    }

    private static IReadOnlyList<string> DeserializeOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string DownloadName(string templateName, string tenantName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var raw = $"{templateName} - {tenantName}.pdf";
        var cleaned = new string(raw.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "form.pdf" : cleaned;
    }
}
