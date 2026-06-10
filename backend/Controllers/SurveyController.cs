using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMEProje.Data;
using UMEProje.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using Microsoft.AspNetCore.Authorization;

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
        /// SADECE "Engineer" ROLÜNDEKİLER ERİŞEBİLİR
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <returns>Güncellenmiş anket</returns>
        [HttpPost("{id}/toggle-approval")]
        [Authorize(Roles = "Engineer")] // 🔒 Güvenlik Kilidi!
        public async Task<IActionResult> ToggleApproval(int id)
        {
            try
            {
                var survey = await _context.CalibrationSurveys.FindAsync(id);
                if (survey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                // Dışarıdan veri beklemek yerine, mevcut durumu otomatik tersine çeviriyoruz (True ise False, False ise True yap)
                survey.IsApproved = !survey.IsApproved;
                survey.Status = survey.IsApproved ? "Approved" : "Rejected";
                survey.UpdatedAt = DateTime.UtcNow;

                _context.CalibrationSurveys.Update(survey);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = survey.IsApproved ? "Anket onaylandı" : "Anket reddedildi", 
                    data = survey 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Hata oluştu", error = ex.Message });
            }
        }

        /// <summary>
        /// Onaylanan anket için kalibrasyon sertifikası oluştur ve indir
        /// </summary>
        /// <param name="id">Anket ID</param>
        /// <returns>PDF sertifikası</returns>
        [HttpGet("{id}/certificate")]
        public async Task<IActionResult> GetCertificate(int id)
        {
            try
            {
                var survey = await _context.CalibrationSurveys
                    .Include(s => s.LabClient)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "Anket bulunamadı" });

                if (!survey.IsApproved)
                    return BadRequest(new { message = "Sadece onaylı anketler için sertifika oluşturulabilir" });

                // QuestPDF ile PDF oluştur
                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);

                        page.Content().Column(col =>
                        {
                            // Başlık
                            col.Item().AlignCenter().Text("KALİBRASYON SERTİFİKASI")
                                .FontSize(24)
                                .Bold()
                                .FontColor("#1F2937");

                            col.Item().PaddingVertical(5).AlignCenter().Text("TUBITAK UME")
                                .FontSize(14)
                                .FontColor("#6B7280");

                            col.Item().PaddingBottom(30).Text("");

                            // Sertifika Numarası ve Tarih
                            col.Item().PaddingBottom(30).Row(row =>
                            {
                                row.RelativeItem().Column(innerCol =>
                                {
                                    innerCol.Item().Text("Sertifika No:").FontSize(10).FontColor("#6B7280");
                                    innerCol.Item().PaddingTop(3).Text($"UME-{survey.Id:D6}").FontSize(12).Bold();
                                });

                                row.RelativeItem().AlignRight().Column(innerCol =>
                                {
                                    innerCol.Item().Text("Tarih:").FontSize(10).FontColor("#6B7280");
                                    innerCol.Item().PaddingTop(3).Text(DateTime.UtcNow.ToString("dd.MM.yyyy")).FontSize(12).Bold();
                                });
                            });

                            // Bilgi Kutusu
                            col.Item().PaddingBottom(30).Border(1).BorderColor("#E5E7EB").Padding(20).Column(infoCol =>
                            {
                                infoCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(innerCol =>
                                    {
                                        innerCol.Item().Text("Firma Adı").FontSize(10).FontColor("#6B7280");
                                        innerCol.Item().PaddingTop(3).PaddingBottom(15).Text(survey.LabClient?.CompanyName ?? "N/A").FontSize(12).Bold();
                                        innerCol.Item().Text("Vergi Numarası").FontSize(10).FontColor("#6B7280");
                                        innerCol.Item().PaddingTop(3).Text(survey.LabClient?.TaxNumber ?? "N/A").FontSize(12).Bold();
                                    });

                                    row.RelativeItem().Column(innerCol =>
                                    {
                                        innerCol.Item().Text("Cihaz Adı").FontSize(10).FontColor("#6B7280");
                                        innerCol.Item().PaddingTop(3).PaddingBottom(15).Text(survey.DeviceName).FontSize(12).Bold();
                                        innerCol.Item().Text("Kategori").FontSize(10).FontColor("#6B7280");
                                        innerCol.Item().PaddingTop(3).Text(survey.LabCategory).FontSize(12).Bold();
                                    });
                                });
                            });

                            // Onay Metni
                            col.Item().PaddingBottom(40).Text(
                                "Bu sertifika, işaretlenen cihazın TUBITAK Ulusal Metroloji Enstitüsü (UME) tarafından kalibrasyon prosesinden başarıyla geçtiğini " +
                                "ve belirtilen laboratuvar kategorisinde yeterli olduğunu doğrular."
                            ).FontSize(10).LineHeight(1.5f).FontColor("#374151");

                            // İmza Bölümü
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(innerCol =>
                                {
                                    innerCol.Item().Text("Mühendis İmzası").FontSize(10).FontColor("#6B7280");
                                    innerCol.Item().PaddingTop(40).BorderTop(1).BorderColor("#E5E7EB").Text("");
                                });

                                row.RelativeItem().Column(innerCol =>
                                {
                                    innerCol.Item().AlignCenter().Text("Kurum Mühür").FontSize(10).FontColor("#6B7280");
                                    innerCol.Item().PaddingTop(40).BorderTop(1).BorderColor("#E5E7EB").Text("");
                                });

                                row.RelativeItem().Column(innerCol =>
                                {
                                    innerCol.Item().AlignRight().Text("Onay Tarihi").FontSize(10).FontColor("#6B7280");
                                    innerCol.Item().PaddingTop(40).BorderTop(1).BorderColor("#E5E7EB").Text("");
                                });
                            });

                            // Altbilgi
                            col.Item().PaddingTop(60).AlignCenter().Text("TUBITAK Ulusal Metroloji Enstitüsü")
                                .FontSize(8)
                                .FontColor("#9CA3AF");
                        });
                    });
                }).GeneratePdf();

                return File(pdfBytes, "application/pdf", $"Sertifika-{survey.Id}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Sertifika oluşturma hatası", error = ex.Message });
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
