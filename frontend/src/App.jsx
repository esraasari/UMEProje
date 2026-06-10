import React, { useEffect, useMemo, useState } from "react";
import {
  AlertCircle,
  Building2,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Download,
  Factory,
  Loader2,
  RefreshCw,
  Search,
  ShieldCheck,
} from "lucide-react";
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import "./App.css";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";
const PAGE_SIZE = 10;

// Mock veri: Cihazın ölçüm sapması (Deviation) verileri
const generateMockData = (surveyId) => [
  { day: "1", deviation: 0.02 + (surveyId % 3) * 0.01 },
  { day: "2", deviation: 0.05 - (surveyId % 2) * 0.02 },
  { day: "3", deviation: -0.01 + (surveyId % 4) * 0.01 },
  { day: "4", deviation: 0.03 - (surveyId % 3) * 0.01 },
  { day: "5", deviation: 0.01 + (surveyId % 5) * 0.01 },
  { day: "6", deviation: -0.02 + (surveyId % 2) * 0.02 },
  { day: "7", deviation: 0.04 - (surveyId % 4) * 0.01 },
];

function normalizeSurvey(survey) {
  return {
    id: survey.id,
    deviceName: survey.deviceName ?? survey.DeviceName ?? "Cihaz adi yok",
    serialNo:
      survey.serialNo ??
      survey.SerialNo ??
      `UME-${String(survey.id).padStart(4, "0")}`,
    labCategory: survey.labCategory ?? survey.LabCategory ?? "Kategori belirtilmedi",
    isApproved: survey.isApproved ?? survey.IsApproved ?? false,
    status: survey.status ?? survey.Status ?? "Pending",
    labClientId: survey.labClientId ?? survey.LabClientId,
  };
}

function normalizeClient(client) {
  const surveys = client.calibrationSurveys ?? client.CalibrationSurveys ?? [];

  return {
    id: client.id,
    companyName: client.companyName ?? client.CompanyName ?? "Firma adi yok",
    taxNumber: client.taxNumber ?? client.TaxNumber ?? "-",
    contactEmail: client.contactEmail ?? client.ContactEmail ?? "-",
    calibrationSurveys: surveys.filter(Boolean).map(normalizeSurvey),
  };
}

function getApprovalRatio(surveys) {
  if (surveys.length === 0) {
    return 0;
  }

  const approvedCount = surveys.filter((survey) => survey.isApproved).length;
  return Math.round((approvedCount / surveys.length) * 100);
}

export default function App() {
  const [clients, setClients] = useState([]);
  const [selectedClientId, setSelectedClientId] = useState(null);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(PAGE_SIZE);
  const [totalItems, setTotalItems] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const [lastUpdatedAt, setLastUpdatedAt] = useState(null);
  const [approvingSurveyId, setApprovingSurveyId] = useState(null);
  const [downloadingSurveyId, setDownloadingSurveyId] = useState(null);

  const selectedClient = useMemo(
    () => clients.find((client) => client.id === selectedClientId) ?? null,
    [clients, selectedClientId]
  );

  const visibleSurveys = selectedClient?.calibrationSurveys ?? [];

  const dashboardStats = useMemo(() => {
    const allSurveys = clients.flatMap((client) => client.calibrationSurveys);
    const approved = allSurveys.filter((survey) => survey.isApproved).length;
    const pending = allSurveys.length - approved;

    return {
      clientCount: totalItems,
      visibleClientCount: clients.length,
      surveyCount: allSurveys.length,
      approved,
      pending,
      approvalRatio: getApprovalRatio(allSurveys),
    };
  }, [clients, totalItems]);

  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      setSearch(searchInput.trim());
    }, 350);

    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    const controller = new AbortController();
    fetchClients(controller.signal);

    return () => controller.abort();
  }, [search, page, pageSize]);

  async function fetchClients(signal) {
    setIsLoading(true);
    setError("");

    try {
      const query = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
      });

      if (search) {
        query.set("search", search);
      }

      const response = await fetch(`${API_BASE_URL}/api/LabClients?${query}`, {
        signal,
      });

      if (!response.ok) {
        throw new Error(`Firma listesi alinamadi. HTTP ${response.status}`);
      }

      const result = await response.json();
      const clientList = Array.isArray(result) ? result : result.data ?? [];
      const normalizedClients = clientList.map(normalizeClient);

      setClients(normalizedClients);
      setTotalItems(Array.isArray(result) ? normalizedClients.length : result.totalItems ?? 0);
      setPage(Array.isArray(result) ? 1 : result.page ?? page);
      setPageSize(Array.isArray(result) ? PAGE_SIZE : result.pageSize ?? pageSize);
      setLastUpdatedAt(new Date());
      setSelectedClientId((currentId) => {
        if (currentId && normalizedClients.some((client) => client.id === currentId)) {
          return currentId;
        }

        return normalizedClients[0]?.id ?? null;
      });
    } catch (fetchError) {
      if (fetchError.name !== "AbortError") {
        setError(fetchError.message || "Beklenmeyen bir hata olustu.");
      }
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }

  async function approveSurvey(surveyId) {
    setApprovingSurveyId(surveyId);
    setError("");

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/Surveys/${surveyId}/toggle-approval`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ isApproved: true }),
        }
      );

      if (!response.ok) {
        const errorBody = await response.json().catch(() => null);
        throw new Error(
          errorBody?.message || `Onay islemi basarisiz. HTTP ${response.status}`
        );
      }

      const result = await response.json();
      const updatedSurvey = normalizeSurvey(result.data ?? result);

      setClients((currentClients) =>
        currentClients.map((client) => ({
          ...client,
          calibrationSurveys: client.calibrationSurveys.map((survey) =>
            survey.id === surveyId
              ? {
                  ...survey,
                  ...updatedSurvey,
                  isApproved: true,
                  status: updatedSurvey.status || "Approved",
                }
              : survey
          ),
        }))
      );
      setLastUpdatedAt(new Date());
    } catch (approvalError) {
      setError(approvalError.message || "Onay islemi tamamlanamadi.");
    } finally {
      setApprovingSurveyId(null);
    }
  }

  function refreshClients() {
    fetchClients();
  }

  async function downloadCertificate(surveyId) {
    setDownloadingSurveyId(surveyId);
    
    try {
      const response = await fetch(
        `${API_BASE_URL}/api/Surveys/${surveyId}/certificate`
      );

      if (!response.ok) {
        throw new Error(
          `Sertifika indirilirken hata oluştu. HTTP ${response.status}`
        );
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `Sertifika-${surveyId}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(downloadError.message || "Sertifika indirilemedi.");
    } finally {
      setDownloadingSurveyId(null);
    }
  }

  return (
    <main className="app-shell">
      <section className="panel">
        <aside className="client-sidebar" aria-label="Firma listesi">
          <div className="brand-block">
            <div className="brand-mark">
              <ShieldCheck size={24} strokeWidth={2.2} />
            </div>
            <div>
              <p className="eyebrow">TUBITAK UME</p>
              <h1>Muhendis Onay Paneli</h1>
            </div>
          </div>

          <label className="search-control">
            <Search size={18} />
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Firma veya vergi no ara"
              type="search"
            />
          </label>

          <div className="sidebar-meta">
            <span>{dashboardStats.clientCount} firma</span>
            <span>{dashboardStats.pending} bekleyen</span>
          </div>

          {isLoading ? (
            <div className="state-box">
              <Loader2 className="spin" size={20} />
              Firmalar yukleniyor
            </div>
          ) : (
            <div className="client-list">
              {clients.map((client, index) => {
                const isActive = client.id === selectedClientId;
                const pendingSurveys = client.calibrationSurveys.filter(
                  (survey) => !survey.isApproved
                ).length;
                const Icon = index % 2 === 0 ? Building2 : Factory;

                return (
                  <button
                    className={`client-button ${isActive ? "active" : ""}`}
                    key={client.id}
                    onClick={() => setSelectedClientId(client.id)}
                    type="button"
                  >
                    <span className="client-icon">
                      <Icon size={20} />
                    </span>
                    <span className="client-copy">
                      <strong>{client.companyName}</strong>
                      <small>VKN {client.taxNumber}</small>
                    </span>
                    <span className="client-count">{pendingSurveys}</span>
                  </button>
                );
              })}

              {clients.length === 0 && (
                <div className="state-box">
                  <AlertCircle size={20} />
                  Kayit bulunamadi
                </div>
              )}
            </div>
          )}

          <div className="pagination-bar">
            <button
              disabled={page <= 1 || isLoading}
              onClick={() => setPage((currentPage) => Math.max(1, currentPage - 1))}
              type="button"
              aria-label="Onceki sayfa"
            >
              <ChevronLeft size={18} />
            </button>
            <span>
              {page} / {totalPages}
            </span>
            <button
              disabled={page >= totalPages || isLoading}
              onClick={() =>
                setPage((currentPage) => Math.min(totalPages, currentPage + 1))
              }
              type="button"
              aria-label="Sonraki sayfa"
            >
              <ChevronRight size={18} />
            </button>
          </div>
        </aside>

        <section className="detail-pane">
          <header className="topbar">
            <div>
              <p className="eyebrow">Kalibrasyon operasyonlari</p>
              <h2>{selectedClient?.companyName ?? "Firma seciniz"}</h2>
            </div>
            <button
              className="icon-action"
              disabled={isLoading}
              onClick={refreshClients}
              type="button"
              aria-label="Verileri yenile"
            >
              <RefreshCw className={isLoading ? "spin" : ""} size={19} />
            </button>
          </header>

          {error && (
            <div className="error-banner" role="alert">
              <span>{error}</span>
              <button type="button" onClick={refreshClients}>
                Yenile
              </button>
            </div>
          )}

          <section className="kpi-grid" aria-label="Operasyon ozeti">
            <div className="kpi-card">
              <span>Toplam Firma</span>
              <strong>{dashboardStats.clientCount}</strong>
              <small>{dashboardStats.visibleClientCount} kayit goruntuleniyor</small>
            </div>
            <div className="kpi-card">
              <span>Bekleyen Onay</span>
              <strong>{dashboardStats.pending}</strong>
              <small>Aktif sayfadaki cihazlar</small>
            </div>
            <div className="kpi-card">
              <span>Onay Orani</span>
              <strong>{dashboardStats.approvalRatio}%</strong>
              <small>{dashboardStats.approved} onayli kayit</small>
            </div>
          </section>

          {!isLoading && selectedClient && (
            <section className="client-summary">
              <div>
                <span>Vergi No</span>
                <strong>{selectedClient.taxNumber}</strong>
              </div>
              <div>
                <span>Iletisim</span>
                <strong>{selectedClient.contactEmail}</strong>
              </div>
              <div>
                <span>Cihaz Kaydi</span>
                <strong>{visibleSurveys.length}</strong>
              </div>
              <div>
                <span>Son Guncelleme</span>
                <strong>
                  {lastUpdatedAt
                    ? lastUpdatedAt.toLocaleTimeString("tr-TR", {
                        hour: "2-digit",
                        minute: "2-digit",
                      })
                    : "-"}
                </strong>
              </div>
            </section>
          )}

          <section className="worklist">
            <div className="section-title">
              <div>
                <p className="eyebrow">Cihaz anketleri</p>
                <h3>{visibleSurveys.length} kalibrasyon kaydi</h3>
              </div>
              <div className="status-chip">
                <ClipboardCheck size={18} />
                {visibleSurveys.filter((survey) => !survey.isApproved).length} bekleyen
              </div>
            </div>

            <div className="survey-list">
              {!isLoading &&
                visibleSurveys.map((survey) => (
                  <article className="survey-row" key={survey.id}>
                    <div className="survey-main">
                      <span
                        className={`approval-dot ${
                          survey.isApproved ? "approved" : "pending"
                        }`}
                      />
                      <div>
                        <h4>{survey.deviceName}</h4>
                        <div className="survey-chart">
                          <ResponsiveContainer width="100%" height={180}>
                            <LineChart data={generateMockData(survey.id)}>
                              <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" />
                              <XAxis dataKey="day" stroke="#6B7280" />
                              <YAxis stroke="#6B7280" />
                              <Tooltip
                                contentStyle={{
                                  backgroundColor: "#ffffff",
                                  border: "1px solid #E5E7EB",
                                  borderRadius: "6px",
                                }}
                                formatter={(value) => `${value.toFixed(3)} mm`}
                                labelFormatter={(label) => `Gün ${label}`}
                              />
                              <Line
                                type="monotone"
                                dataKey="deviation"
                                stroke="#DC2626"
                                strokeWidth={2}
                                dot={{ fill: "#DC2626", r: 4 }}
                                activeDot={{ r: 6 }}
                              />
                            </LineChart>
                          </ResponsiveContainer>
                        </div>
                        <div className="survey-meta">
                          <span>Seri No: {survey.serialNo}</span>
                          <span>{survey.labCategory}</span>
                          <span>Durum: {survey.status}</span>
                        </div>
                      </div>
                    </div>

                    <div className="survey-actions">
                      <span
                        className={`approval-label ${
                          survey.isApproved ? "approved" : "pending"
                        }`}
                      >
                        {survey.isApproved ? "Onaylandi" : "Onay bekliyor"}
                      </span>

                      {!survey.isApproved && (
                        <button
                          className="approve-button"
                          disabled={approvingSurveyId === survey.id}
                          onClick={() => approveSurvey(survey.id)}
                          type="button"
                        >
                          {approvingSurveyId === survey.id ? (
                            <Loader2 className="spin" size={20} />
                          ) : (
                            <CheckCircle2 size={20} />
                          )}
                          Onayla
                        </button>
                      )}

                      {survey.isApproved && (
                        <button
                          className="certificate-button"
                          disabled={downloadingSurveyId === survey.id}
                          onClick={() => downloadCertificate(survey.id)}
                          type="button"
                          title="Sertifikayı indir"
                        >
                          {downloadingSurveyId === survey.id ? (
                            <Loader2 className="spin" size={20} />
                          ) : (
                            <Download size={20} />
                          )}
                          Sertifika İndir
                        </button>
                      )}
                    </div>
                  </article>
                ))}

              {!isLoading && selectedClient && visibleSurveys.length === 0 && (
                <div className="empty-state">
                  Bu firmaya ait kalibrasyon anketi bulunmuyor.
                </div>
              )}

              {!isLoading && !selectedClient && (
                <div className="empty-state">Islem yapmak icin firma seciniz.</div>
              )}
            </div>
          </section>
        </section>
      </section>
    </main>
  );
}
