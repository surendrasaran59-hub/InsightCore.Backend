using DocumentFormat.OpenXml.Spreadsheet;
using InsightCore.Application.Interfaces;
using InsightCore.Shared.DTO;
using InsightCore.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net.Http;
using System.Net.Http.Headers;

namespace InsightCore.Web.Pages.DataModel
{
    public class UploadModel : PageModel
    {

        private readonly HttpClient _httpClient;
        private readonly ILogger<UploadModel> _logger;
        private const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

        public UploadModel(IHttpClientFactory httpClientFactory, ILogger<UploadModel> logger)
        {
            _httpClient = httpClientFactory.CreateClient("InsightCoreApi");
            _logger = logger;
        }

        [BindProperty]
        public string? Message { get; set; }

        [BindProperty]
        public int? SelectedClientId { get; set; }

        [BindProperty]
        public string? SelectedClientName { get; set; } = string.Empty;

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
            else
            {
                var ext = Path.GetExtension(UploadFile.FileName).ToLowerInvariant();
                var allowed = new[] { ".csv", ".xls", ".xlsx" };
                if (!allowed.Contains(ext))
                    ModelState.AddModelError(nameof(UploadFile), "Only CSV, XLS, or XLSX files are allowed.");
            }

            if (!ModelState.IsValid)
                return Page();

            try
            {
                int userId = 1; // need to fix
                string clientName = string.Empty;
                if (!string.IsNullOrEmpty(SelectedClientName))
                {
                    clientName = SelectedClientName;
                }

                var apiResponse = await CallUploadApiAsync(SelectedClientId.Value, UploadFile, clientName, userId);
                if (!apiResponse.IsSuccessStatusCode)
                {
                    var errorBody = await apiResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Upload API failed — Status: {StatusCode}, Body: {Body}", apiResponse.StatusCode, errorBody);
                    ModelState.AddModelError(string.Empty, $"Schema generation request failed: {apiResponse.ReasonPhrase}");
                    return Page();
                }

                var blobName = UploadFileValidator.BuildBlobName(SelectedClientId.Value, clientName, UploadFile.FileName);

                var newID = await InsertClientDataModelAsync(new ClientDataModel
                {
                    ClientId = SelectedClientId.Value,
                    FileName = blobName, //UploadFile.FileName,
                    CreatedBy = userId,
                    UploadStatus = "Uploaded",
                    ProcessingStatus = "Pending"
                });

                TempData["SuccessMessage"] = "File uploaded successfully!";
                _logger.LogInformation("Upload API called successfully for ClientId {ClientId}.", SelectedClientId.Value);


                //var serviceBusClient = new ServiceBusClient(connectionString);
                //var sender = serviceBusClient.CreateSender("validation-queue");
                //var message = new ServiceBusMessage { Body = BinaryData.FromString(file.FileName), ApplicationProperties = { ["ClientId"] = clientId } };
                //await sender.SendMessageAsync(message);
                //return Ok();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "File upload failed!";

                _logger.LogError(ex, "Blob upload failed for client {ClientId}", SelectedClientId);
                ModelState.AddModelError(string.Empty, "File upload failed. Please try again.");
                return Page();
            }

            return Page();

            // Handle file upload here
            //return RedirectToPage("Success");
        }

        private async Task<List<SelectListItem>> LoadClientsAsync()
        {
            var clients = await _httpClient.GetFromJsonAsync<List<ClientDto>>("api/client/getclients") ?? new();
            return clients.Select(c => new SelectListItem
            {
                Value = c.ClientId.ToString(),
                Text = c.ClientName
            }).ToList();
        }

        private async Task<HttpResponseMessage> CallUploadApiAsync(int clientId, IFormFile file, string clientName, int userId, CancellationToken cancellationToken = default)
        {
            using var content = new MultipartFormDataContent();

            // Add clientId
            content.Add(new StringContent(clientId.ToString()), "clientId");
            // Add clientName
            content.Add(new StringContent(clientName), "clientName");
            // Add userId
            content.Add(new StringContent(userId.ToString()), "userId");

            // Add file
            using var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

            content.Add(fileContent, "file", file.FileName);

            return await _httpClient.PostAsync("api/datamodelupload/upload", content, cancellationToken);
        }

        private async Task<int> InsertClientDataModelAsync(ClientDataModel clientDataModel)
        {

            var response = await _httpClient.PostAsJsonAsync("api/client/clientDataModel", clientDataModel);

            response.EnsureSuccessStatusCode(); 

            var newId = await response.Content.ReadFromJsonAsync<int>();
            return newId;
        }

    }
}