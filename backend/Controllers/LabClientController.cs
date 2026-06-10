using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMEProje.Data;
using UMEProje.Models;

namespace UMEProje.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LabClientsController : ControllerBase
    {
        private const int MaxPageSize = 100;
        private static readonly Regex TaxNumberRegex = new(@"^\d{10}$", RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly ILogger<LabClientsController> _logger;

        public LabClientsController(AppDbContext context, ILogger<LabClientsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Tüm kurumsal firmaları sayfalama ve filtreleme desteğiyle listeler.
        /// </summary>
        /// <param name="search">Firma adı veya vergi numarası arama metni.</param>
        /// <param name="page">Sayfa numarası.</param>
        /// <param name="pageSize">Sayfa başına kayıt sayısı.</param>
        /// <returns>Sayfalama bilgileri ve firma listesi.</returns>
        [HttpGet]
        public async Task<IActionResult> GetLabClients(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                page = Math.Max(page, 1);
                pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

                var query = _context.LabClients.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim();
                    query = query.Where(lc =>
                        lc.CompanyName.Contains(searchTerm) ||
                        lc.TaxNumber.Contains(searchTerm));
                }

                var totalItems = await query.CountAsync();

                var labClients = await query
                    .Include(lc => lc.CalibrationSurveys)
                    .OrderBy(lc => lc.CompanyName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    totalItems,
                    page,
                    pageSize,
                    data = labClients
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab client listesi alınırken beklenmeyen bir hata oluştu.");
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen ID ile firma bilgisini getirir.
        /// </summary>
        /// <param name="id">Firma ID.</param>
        /// <returns>Firma bilgileri.</returns>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<LabClient>> GetLabClient(int id)
        {
            try
            {
                var labClient = await _context.LabClients
                    .Include(lc => lc.CalibrationSurveys)
                    .FirstOrDefaultAsync(lc => lc.Id == id);

                if (labClient == null)
                {
                    return NotFound(new { message = "Firma bulunamadı" });
                }

                return Ok(labClient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab client detayı alınırken hata oluştu. LabClientId: {LabClientId}", id);
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir kurumsal firma ekler.
        /// </summary>
        /// <param name="labClient">Firma bilgileri.</param>
        /// <returns>Oluşturulan firma.</returns>
        [HttpPost]
        public async Task<ActionResult<LabClient>> CreateLabClient([FromBody] LabClient labClient)
        {
            try
            {
                var validationError = ValidateLabClient(labClient);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError });
                }

                labClient.CompanyName = labClient.CompanyName.Trim();
                labClient.TaxNumber = labClient.TaxNumber.Trim();
                labClient.ContactEmail = labClient.ContactEmail.Trim();
                labClient.CreatedAt = DateTime.UtcNow;
                labClient.UpdatedAt = null;

                _context.LabClients.Add(labClient);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetLabClient), new { id = labClient.Id }, labClient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yeni lab client oluşturulurken hata oluştu. TaxNumber: {TaxNumber}", labClient.TaxNumber);
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Firma bilgilerini günceller.
        /// </summary>
        /// <param name="id">Firma ID.</param>
        /// <param name="labClient">Güncellenmiş firma bilgileri.</param>
        /// <returns>Güncellenmiş firma.</returns>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLabClient(int id, [FromBody] LabClient labClient)
        {
            try
            {
                var validationError = ValidateLabClient(labClient);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError });
                }

                var existingLabClient = await _context.LabClients.FindAsync(id);
                if (existingLabClient == null)
                {
                    return NotFound(new { message = "Firma bulunamadı" });
                }

                existingLabClient.CompanyName = labClient.CompanyName.Trim();
                existingLabClient.TaxNumber = labClient.TaxNumber.Trim();
                existingLabClient.ContactEmail = labClient.ContactEmail.Trim();
                existingLabClient.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Firma başarıyla güncellendi", data = existingLabClient });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab client güncellenirken hata oluştu. LabClientId: {LabClientId}", id);
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Firmayı siler.
        /// </summary>
        /// <param name="id">Firma ID.</param>
        /// <returns>Silme işlem sonucu.</returns>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLabClient(int id)
        {
            try
            {
                var labClient = await _context.LabClients.FindAsync(id);
                if (labClient == null)
                {
                    return NotFound(new { message = "Firma bulunamadı" });
                }

                _context.LabClients.Remove(labClient);
                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "Lab client silindi. LabClientId: {LabClientId}, CompanyName: {CompanyName}, TaxNumber: {TaxNumber}",
                    labClient.Id,
                    labClient.CompanyName,
                    labClient.TaxNumber);

                return Ok(new { message = "Firma başarıyla silindi" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab client silinirken hata oluştu. LabClientId: {LabClientId}", id);
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        private static string? ValidateLabClient(LabClient labClient)
        {
            if (labClient == null)
            {
                return "Firma bilgileri zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(labClient.CompanyName))
            {
                return "CompanyName zorunludur.";
            }

            if (string.IsNullOrWhiteSpace(labClient.TaxNumber))
            {
                return "TaxNumber zorunludur.";
            }

            if (!TaxNumberRegex.IsMatch(labClient.TaxNumber.Trim()))
            {
                return "TaxNumber tam 10 haneli olmalı ve sadece rakamlardan oluşmalıdır.";
            }

            if (string.IsNullOrWhiteSpace(labClient.ContactEmail))
            {
                return "ContactEmail zorunludur.";
            }

            if (!IsValidEmail(labClient.ContactEmail.Trim()))
            {
                return "ContactEmail geçerli bir e-posta formatında olmalıdır.";
            }

            return null;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var mailAddress = new MailAddress(email);
                return mailAddress.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
