using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMEProje.Data;
using UMEProje.Models;

namespace UMEProje.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SurveysController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SurveysController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tüm kalibrasyon anketlerini listele
        /// </summary>
        /// <returns>Kalibrasyon anketleri listesi</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CalibrationSurvey>>> GetCalibrationSurveys()
        {
            try
            {
                var surveys = await _context.CalibrationSurveys
                    .Include(s => s.LabClient)
                    .ToListAsync();

                return Ok(surveys);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Belirtilen ID ile anketi getir
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <returns>Anket bilgileri</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<CalibrationSurvey>> GetCalibrationSurvey(int id)
        {
            try
            {
                var survey = await _context.CalibrationSurveys
                    .Include(s => s.LabClient)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                return Ok(survey);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Yeni bir kalibrasyon anketi ekle (dış firmalardan cihaz kalibrasyon talebi)
        /// </summary>
        /// <param name="survey">Anket bilgileri</param>
        /// <returns>Oluşturulan anket</returns>
        [HttpPost]
        public async Task<ActionResult<CalibrationSurvey>> CreateCalibrationSurvey([FromBody] CalibrationSurvey survey)
        {
            try
            {
                // Validasyon
                if (string.IsNullOrEmpty(survey.DeviceName) || 
                    string.IsNullOrEmpty(survey.LabCategory))
                {
                    return BadRequest(new { message = "DeviceName ve LabCategory zorunludur" });
                }

                // LabClient varlığını kontrol et
                var labClientExists = await _context.LabClients.AnyAsync(lc => lc.Id == survey.LabClientId);
                if (!labClientExists)
                {
                    return BadRequest(new { message = "Belirtilen firma (LabClient) bulunamadı" });
                }

                survey.CreatedAt = DateTime.UtcNow;
                survey.Status = "Pending";
                survey.IsApproved = false;

                _context.CalibrationSurveys.Add(survey);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCalibrationSurvey), new { id = survey.Id }, survey);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Anketi güncelle
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <param name="survey">Güncellenmiş anket bilgileri</param>
        /// <returns>Güncellenmiş anket</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCalibrationSurvey(int id, [FromBody] CalibrationSurvey survey)
        {
            try
            {
                var existingSurvey = await _context.CalibrationSurveys.FindAsync(id);
                if (existingSurvey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                existingSurvey.DeviceName = survey.DeviceName ?? existingSurvey.DeviceName;
                existingSurvey.LabCategory = survey.LabCategory ?? existingSurvey.LabCategory;
                existingSurvey.Status = survey.Status ?? existingSurvey.Status;
                existingSurvey.UpdatedAt = DateTime.UtcNow;

                _context.CalibrationSurveys.Update(existingSurvey);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Anket başarıyla güncellendi", data = existingSurvey });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// UME mühendisinin anketi onaylaması/reddetmesi (IsApproved toggle)
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <param name="request">Onay durumu (IsApproved: true/false)</param>
        /// <returns>Güncellenmiş anket</returns>
        [HttpPost("{id}/toggle-approval")]
        public async Task<IActionResult> ToggleApproval(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var survey = await _context.CalibrationSurveys.FindAsync(id);
                if (survey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                survey.IsApproved = request.IsApproved;
                survey.Status = request.IsApproved ? "Approved" : "Rejected";
                survey.UpdatedAt = DateTime.UtcNow;

                _context.CalibrationSurveys.Update(survey);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = request.IsApproved ? "Anket onaylandı" : "Anket reddedildi", 
                    data = survey 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Anketi sil
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <returns>Silme işlem sonucu</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCalibrationSurvey(int id)
        {
            try
            {
                var survey = await _context.CalibrationSurveys.FindAsync(id);
                if (survey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                _context.CalibrationSurveys.Remove(survey);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Anket başarıyla silindi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Onay talebi için request modeli
    /// </summary>
    public class ApprovalRequest
    {
        public bool IsApproved { get; set; }
    }
}
