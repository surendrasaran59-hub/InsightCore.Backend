using DocumentFormat.OpenXml.Spreadsheet;
using InsightCore.Application.Interfaces;
using InsightCore.Shared.DTO;
using InsightCore.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InsightCore.Web.Pages.DataModel
{
    public class UploadModel : PageModel
    {
        // Allowed MIME types 
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
            "application/vnd.ms-excel",                                           // .xls
            "text/csv",                                                            // .csv
            "application/csv",
            "text/plain",      // some browsers send this for .csv
        };

        private readonly HttpClient _httpClient;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<UploadModel> _logger;
        private readonly IEntitySchemaGenerator _schemaGenerator;   
        private const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

        public UploadModel(IHttpClientFactory httpClientFactory, IBlobStorageService blobStorageService, ILogger<UploadModel> logger, IEntitySchemaGenerator schemaGenerator)
        {
            _httpClient = httpClientFactory.CreateClient();
            _blobStorageService = blobStorageService;
            _logger = logger;
            _schemaGenerator = schemaGenerator;
        }

        [BindProperty]
        public string? Message { get; set; }

        [BindProperty]
        public int? SelectedClientId { get; set; }

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

        public List<SelectListItem> Clients { get; set; } = new();

        public async Task OnGetAsync()
        {
            Clients = await LoadClientsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Clients = await LoadClientsAsync();

            if (SelectedClientId == null)
                ModelState.AddModelError(nameof(SelectedClientId), "Please select a client.");

            if (UploadFile == null || UploadFile.Length == 0)
            {
                ModelState.AddModelError(nameof(UploadFile), "Please upload a file.");
            }
            else if (UploadFile.Length > MaxFileSizeBytes) // File size validation
            {
                ModelState.AddModelError(nameof(UploadFile), "File exceeds the 50 MB size limit.");
            }
            else if (!AllowedMimeTypes.Contains(UploadFile.ContentType)) //MIME type validation
            {
                _logger.LogWarning("Upload rejected — disallowed MIME type: {Mime}", UploadFile.ContentType);
                ModelState.AddModelError(nameof(UploadFile), $"File type '{UploadFile.ContentType}' is not allowed. Upload Excel (.xlsx/.xls) or CSV files only.");                
            }
            else
            {
                var ext = Path.GetExtension(UploadFile.FileName).ToLowerInvariant();
                var allowed = new[] { ".csv", ".xls", ".xlsx" };
                if (!allowed.Contains(ext))
                    ModelState.AddModelError(nameof(UploadFile), "Only CSV, XLS, or XLSX files are allowed.");

                // Byte-stream (magic-byte) validation
                await using var stream = UploadFile.OpenReadStream();
                var (isValid, streamValidationError) = await UploadFileValidator.ValidateFileStreamAsync(stream, UploadFile.FileName);

                if (!isValid)
                {
                    _logger.LogWarning("Upload rejected — stream validation failed for '{File}': {Reason}", UploadFile.FileName, streamValidationError);
                    ModelState.AddModelError(nameof(UploadFile), streamValidationError);
                    return Page();
                }
            }

            if (!ModelState.IsValid)
                return Page();

            // Upload to Azure Blob Storage
            try
            {
                int userId = 1;
                var blobName = $"{SelectedClientId}/{Guid.NewGuid()}_{UploadFile.FileName}";

                await _blobStorageService.UploadFileAsync(
                    containerName: "client-uploads",
                    blobName: blobName,
                    stream: UploadFile.OpenReadStream(),
                    contentType: UploadFile.ContentType
                );

                var newID = await InsertClientDataModelAsync(new ClientDataModel
                {
                    ClientId = SelectedClientId.Value,
                    FileName = UploadFile.FileName,
                    CreatedBy = userId,
                    UploadStatus = "Uploaded",
                    ProcessingStatus = "Pending"
                });

                // call this method to generate the schema and store it in the database. 
                //string result = _schemaGenerator.GenerateSchema(SelectedClientId.Value, userId, blobName);

                //var serviceBusClient = new ServiceBusClient(connectionString);
                //var sender = serviceBusClient.CreateSender("validation-queue");
                //var message = new ServiceBusMessage { Body = BinaryData.FromString(file.FileName), ApplicationProperties = { ["ClientId"] = clientId } };
                //await sender.SendMessageAsync(message);
                //return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blob upload failed for client {ClientId}", SelectedClientId);
                ModelState.AddModelError(string.Empty, "File upload failed. Please try again.");
                return Page();
            }

            Message = "File has been uploaded successfully. Now generating schema!!!";
            return Page();

            // Handle file upload here
            //return RedirectToPage("Success");
        }

        private async Task<List<SelectListItem>> LoadClientsAsync()
        {
            var clients = await _httpClient.GetFromJsonAsync<List<ClientDto>>("https://localhost:7226/api/client/getclients") ?? new();
            return clients.Select(c => new SelectListItem
            {
                Value = c.ClientId.ToString(),
                Text = c.ClientName
            }).ToList();
        }

        private async Task<int> InsertClientDataModelAsync(ClientDataModel clientDataModel)
        {
            var response = await _httpClient.PostAsJsonAsync(
        "https://localhost:7226/api/client/clientDataModel", clientDataModel);

            response.EnsureSuccessStatusCode(); // throws HttpRequestException on 4xx/5xx

            var newId = await response.Content.ReadFromJsonAsync<int>();
            return newId;
        }

    }
}