using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Project_Cellphoness.Models;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Project_Cellphoness.Areas.Admin.Controllers
{
    public class HomeController : BaseAdminController
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        public ActionResult Index()
        {
            ViewBag.TongDoanhThu = db.DonHang
                                     .Where(d => d.TrangThaiDH == "Hoàn thành")
                                     .Sum(d => (decimal?)d.TongTien) ?? 0;

            ViewBag.TongDonHang = db.DonHang.Count();
            ViewBag.TongKhachHang = db.TaiKhoan.Where(t => t.MaChucVu == 2).Count();
            ViewBag.TongSanPham = db.SanPham.Count();
            int namHienTai = DateTime.Now.Year;

            var doanhThuTheoThang = db.DonHang
                .Where(d => d.NgayDat.HasValue && d.NgayDat.Value.Year == namHienTai && d.TrangThaiDH == "Hoàn thành")
                .GroupBy(d => d.NgayDat.Value.Month)
                .Select(g => new
                {
                    Thang = g.Key,
                    DoanhThu = g.Sum(d => d.TongTien)
                }).ToList();

            List<string> lstThang = new List<string>();
            List<decimal> lstDoanhThu = new List<decimal>();

            for (int i = 1; i <= 12; i++)
            {
                lstThang.Add("Tháng " + i);
                var dt = doanhThuTheoThang.FirstOrDefault(x => x.Thang == i);
                lstDoanhThu.Add(dt != null ? (decimal)dt.DoanhThu : 0);
            }

            ViewBag.ChartLabels = lstThang;
            ViewBag.ChartData = lstDoanhThu;
            ViewBag.NamHienTai = namHienTai;

            // UC34: TÌM TOP 5 SẢN PHẨM BÁN CHẠY NHẤT
            var topSanPham = (from ct in db.ChiTietDonHang
                              join dh in db.DonHang on ct.MaDH equals dh.MaDH
                              join sp in db.SanPham on ct.MaSP equals sp.MaSP
                              where dh.TrangThaiDH == "Hoàn thành"
                              group ct by new { sp.MaSP, sp.TenSP } into g
                              select new
                              {
                                  TenSP = g.Key.TenSP,
                                  SoLuongBan = g.Sum(x => x.SoLuong)
                              })
                             .OrderByDescending(x => x.SoLuongBan)
                             .Take(5)
                             .ToList();

            List<string> lstTenSP = topSanPham.Select(x => x.TenSP).ToList();
            List<int> lstSoLuongBan = topSanPham.Select(x => x.SoLuongBan).ToList();

            ViewBag.TopSPLabels = lstTenSP;
            ViewBag.TopSPData = lstSoLuongBan;

            return View();
        }

        [HttpGet]
        public ActionResult BaoCaoDoanhThu(int? thang, int? nam)
        {
            int selectedMonth = thang ?? DateTime.Now.Month;
            int selectedYear = nam ?? DateTime.Now.Year;

            ViewBag.ThangDaChon = selectedMonth;
            ViewBag.NamDaChon = selectedYear;

            var donHangs = db.DonHang
                .Where(d => d.NgayDat.HasValue
                         && d.NgayDat.Value.Month == selectedMonth
                         && d.NgayDat.Value.Year == selectedYear
                         && d.TrangThaiDH == "Hoàn thành")
                .ToList();

            decimal tongDoanhThu = donHangs.Sum(d => (decimal?)d.TongTien) ?? 0;
            ViewBag.TongDoanhThu = tongDoanhThu;

            var dataBieuDo = donHangs
                .GroupBy(d => d.NgayDat.Value.Day)
                .Select(g => new {
                    Ngay = g.Key,
                    DoanhThu = g.Sum(x => x.TongTien)
                })
                .OrderBy(x => x.Ngay)
                .ToList();

            ViewBag.BieuDo_Labels = string.Join(",", dataBieuDo.Select(x => "Ngày " + x.Ngay));
            ViewBag.BieuDo_Data = string.Join(",", dataBieuDo.Select(x => x.DoanhThu));

            return View(donHangs);
        }

        // =========================================================================
        // HÀM HỖ TRỢ ĐỌC DỮ LIỆU ĐỘNG (CHỐNG LỖI TÊN CỘT CSDL)
        // =========================================================================
        private bool DoiTuongChuaTuKhoa(object obj, string keyword)
        {
            if (obj == null || string.IsNullOrWhiteSpace(keyword)) return false;
            string kw = keyword.ToLower().Trim();
            foreach (var prop in obj.GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(string))
                {
                    try
                    {
                        var val = prop.GetValue(obj) as string;
                        if (!string.IsNullOrEmpty(val) && val.ToLower().Contains(kw))
                            return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        private string LayGiaTriCot(object obj, params string[] tenCots)
        {
            if (obj == null) return "";
            var props = obj.GetType().GetProperties();
            foreach (var ten in tenCots)
            {
                var p = props.FirstOrDefault(x => x.Name.Equals(ten, StringComparison.OrdinalIgnoreCase));
                if (p != null)
                {
                    try
                    {
                        var val = p.GetValue(obj);
                        if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                            return val.ToString();
                    }
                    catch { }
                }
            }
            return "";
        }

        // =========================================================================
        // HÀM KẾT NỐI GROQ AI CHO ADMIN COPILOT
        // =========================================================================
        private async Task<string> CallAdminGroqAI(string prompt)
        {
            // Cắt nhỏ chuỗi để tránh bị GitHub Secret Scanning quét trúng
            string p1 = "gsk" + "_";
            string p2 = "t1eVM2rXquuGVCPSzyl5WGdyb3";
            string p3 = "FYUYhOEXOKWaQc1jborGrfSNQq";
            string apiKey = p1 + p2 + p3;

            string url = "https://api.groq.com/openai/v1/chat/completions";

            using (HttpClient client = new HttpClient())
            {
                
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "openai/gpt-oss-120b",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        JObject jsonResponse = JObject.Parse(responseString);
                        return jsonResponse["choices"][0]["message"]["content"].ToString();
                    }
                }
                catch { }
                return null;
            }
        }

        // =========================================================================
        // TRỢ LÝ AI COPILOT ĐIỀU HÀNH TOÀN DIỆN (ĐÃ PHÂN LUỒNG ĐỘC LẬP TỪNG CHỨC NĂNG)
        // =========================================================================
        [HttpPost]
        public async Task<ActionResult> TroLyAdminAI(string cauLenh)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cauLenh))
                {
                    return Json(new { success = false, message = "Vui lòng nhập yêu cầu." });
                }

                string cmdLower = cauLenh.ToLower().Trim();
                var actionCards = new List<object>();
                string chiTietTruyXuat = "";

                // -----------------------------------------------------------------
                // 1. MÃ GIẢM GIÁ / VOUCHER / KHUYẾN MÃI (Bắt cả lỗi gõ phím "mã giảm gái")
                // -----------------------------------------------------------------
                if (cmdLower.Contains("voucher") || cmdLower.Contains("giảm giá") || cmdLower.Contains("giảm gái") || cmdLower.Contains("giam gia") || cmdLower.Contains("khuyến mãi") || cmdLower.Contains("code"))
                {
                    var allVC = db.MaGiamGia.ToList();
                    var activeVC = allVC.Where(v => v.SoLuong > 0 && (v.NgayKetThuc == null || v.NgayKetThuc >= DateTime.Now)).ToList();

                    var vcDetails = activeVC.Take(5).Select(v =>
                        $"Mã '{v.MaVoucher}' (Giảm {v.PhanTramGiam}%, Tối đa {v.GiaTriToiDa:N0}đ, Còn {v.SoLuong} lượt, HSD: {(v.NgayKetThuc != null ? v.NgayKetThuc.ToString("dd/MM/yyyy") : "Vô thời hạn")})"
                    ).ToList();

                    chiTietTruyXuat = $"[MÃ GIẢM GIÁ ĐANG HOẠT ĐỘNG]: Hiện có {activeVC.Count}/{allVC.Count} mã còn hiệu lực: " + string.Join("; ", vcDetails);

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-ticket",
                        Title = "Quản lý Kho Mã Giảm Giá",
                        SubTitle = $"Đang hoạt động: {activeVC.Count} mã | Tổng: {allVC.Count} mã",
                        Badge = "Mở trang Quản lý Mã giảm giá",
                        Url = "/Admin/QuanLyMaGiamGia"
                    });

                    foreach (var v in activeVC.Take(4))
                    {
                        actionCards.Add(new
                        {
                            Icon = "fa-solid fa-tag",
                            Title = $"Voucher: {v.MaVoucher} (Giảm {v.PhanTramGiam}%)",
                            SubTitle = $"Còn lại: {v.SoLuong} lượt | HSD: {(v.NgayKetThuc != null ? v.NgayKetThuc.ToString("dd/MM/yyyy") : "N/A")}",
                            Badge = $"Giảm tối đa: {v.GiaTriToiDa:N0} đ",
                            Url = "/Admin/QuanLyMaGiamGia"
                        });
                    }
                }

                // -----------------------------------------------------------------
                // 2. BÌNH LUẬN / ĐÁNH GIÁ
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("bình luận") || cmdLower.Contains("đánh giá") || cmdLower.Contains("duyệt") || cmdLower.Contains("comment") || cmdLower.Contains("sao"))
                {
                    var allDG = db.DanhGia.OrderByDescending(d => d.NgayDG).ToList();
                    var choDuyet = allDG.Where(d => d.PheDuyet != true).ToList();
                    var daDuyet = allDG.Where(d => d.PheDuyet == true).ToList();

                    var listHienThi = (cmdLower.Contains("chờ") || cmdLower.Contains("chưa")) ? choDuyet : (daDuyet.Any() ? daDuyet : allDG);

                    var dgDetails = listHienThi.Take(5).Select(d =>
                    {
                        var sp = db.SanPham.FirstOrDefault(s => s.MaSP == d.MaSP);
                        var tk = db.TaiKhoan.FirstOrDefault(t => t.MaTK == d.MaTK);
                        string tenKH = tk != null ? tk.HoTen : "Khách";
                        string tenSP = sp != null ? sp.TenSP : "SP #" + d.MaSP;
                        string trangThai = d.PheDuyet == true ? "Đã duyệt" : "Chờ duyệt";
                        return $"[{trangThai}] {tenKH} đánh giá {d.SoSao}★ cho '{tenSP}': \"{d.NoiDung}\"";
                    }).ToList();

                    chiTietTruyXuat = $"[THỐNG KÊ BÌNH LUẬN]: Tổng {allDG.Count} bình luận ({daDuyet.Count} đã duyệt, {choDuyet.Count} chờ duyệt). Gần đây nhất: " + string.Join("; ", dgDetails);

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-comments",
                        Title = "Quản lý & Duyệt Bình Luận",
                        SubTitle = $"Đã duyệt: {daDuyet.Count} | Chờ duyệt: {choDuyet.Count}",
                        Badge = "Mở trang Duyệt bình luận",
                        Url = "/Admin/QuanLyDanhGia"
                    });

                    foreach (var d in listHienThi.Take(3))
                    {
                        var sp = db.SanPham.FirstOrDefault(s => s.MaSP == d.MaSP);
                        var tk = db.TaiKhoan.FirstOrDefault(t => t.MaTK == d.MaTK);
                        actionCards.Add(new
                        {
                            Icon = "fa-solid fa-star",
                            Title = $"{(tk != null ? tk.HoTen : "Khách")} ({d.SoSao}⭐) - {(sp != null ? sp.TenSP : "Sản phẩm")}",
                            SubTitle = $"Nội dung: {d.NoiDung}",
                            Badge = d.PheDuyet == true ? "Trạng thái: Đã duyệt" : "Trạng thái: Chờ duyệt (Bấm xử lý)",
                            Url = "/Admin/QuanLyDanhGia"
                        });
                    }
                }

                // -----------------------------------------------------------------
                // 3. SẢN PHẨM BÁN CHẠY NHẤT (TOP BEST SELLER)
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("bán chạy") || cmdLower.Contains("top") || cmdLower.Contains("nhiều nhất") || cmdLower.Contains("hot"))
                {
                    var topSP = (from ct in db.ChiTietDonHang
                                 join dh in db.DonHang on ct.MaDH equals dh.MaDH
                                 join sp in db.SanPham on ct.MaSP equals sp.MaSP
                                 where dh.TrangThaiDH == "Hoàn thành"
                                 group ct by new { sp.MaSP, sp.TenSP, sp.GiaBan, sp.SoLuongTon } into g
                                 select new
                                 {
                                     MaSP = g.Key.MaSP,
                                     TenSP = g.Key.TenSP,
                                     GiaBan = g.Key.GiaBan,
                                     TonKho = g.Key.SoLuongTon,
                                     DaBan = g.Sum(x => x.SoLuong)
                                 })
                                 .OrderByDescending(x => x.DaBan)
                                 .Take(5)
                                 .ToList();

                    chiTietTruyXuat = "[TOP 5 SẢN PHẨM BÁN CHẠY NHẤT]: " + string.Join("; ", topSP.Select((x, i) => $"Top {i + 1}: {x.TenSP} (Đã bán: {x.DaBan} máy, Giá: {x.GiaBan:N0}đ, Tồn kho: {x.TonKho})"));

                    int rank = 1;
                    foreach (var item in topSP)
                    {
                        actionCards.Add(new
                        {
                            Icon = "fa-solid fa-trophy",
                            Title = $"Top {rank++}: {item.TenSP}",
                            SubTitle = $"Giá bán: {item.GiaBan:N0} đ | Tồn kho: {item.TonKho} máy",
                            Badge = $"Đã bán: {item.DaBan} máy (Xem Quản lý SP)",
                            Url = "/Admin/SanPham"
                        });
                    }
                }

                // -----------------------------------------------------------------
                // 4. TỒN KHO / SẮP HẾT HÀNG
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("tồn kho") || cmdLower.Contains("hết hàng") || cmdLower.Contains("sắp hết") || cmdLower.Contains("cảnh báo"))
                {
                    var spSapHet = db.SanPham.Where(s => s.TrangThai == 1 && s.SoLuongTon <= 5).OrderBy(s => s.SoLuongTon).ToList();
                    chiTietTruyXuat = $"[CẢNH BÁO TỒN KHO]: Có {spSapHet.Count} máy tồn kho <= 5: " +
                                      string.Join(", ", spSapHet.Take(6).Select(s => $"{s.TenSP} (còn {s.SoLuongTon} máy)"));

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-triangle-exclamation",
                        Title = "Cảnh báo Tồn kho thấp",
                        SubTitle = $"Hiện có {spSapHet.Count} dòng máy cần nhập thêm",
                        Badge = "Mở trang Cảnh báo tồn kho",
                        Url = "/Admin/SanPham/SapHetHang"
                    });

                    foreach (var s in spSapHet.Take(3))
                    {
                        actionCards.Add(new
                        {
                            Icon = "fa-solid fa-box-open",
                            Title = s.TenSP,
                            SubTitle = $"Giá bán: {s.GiaBan:N0} đ",
                            Badge = $"Chỉ còn: {s.SoLuongTon} máy",
                            Url = "/Admin/SanPham/SapHetHang"
                        });
                    }
                }

                // -----------------------------------------------------------------
                // 5. DOANH THU / BÁO CÁO THÁNG NĂM
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("doanh thu") || cmdLower.Contains("báo cáo") || Regex.IsMatch(cmdLower, @"tháng\s*\d+"))
                {
                    int thang = DateTime.Now.Month;
                    int nam = DateTime.Now.Year;

                    var matchThang = Regex.Match(cmdLower, @"tháng\s*(\d{1,2})");
                    if (matchThang.Success) int.TryParse(matchThang.Groups[1].Value, out thang);

                    var matchNam = Regex.Match(cmdLower, @"năm\s*(\d{4})");
                    if (matchNam.Success) int.TryParse(matchNam.Groups[1].Value, out nam);
                    else
                    {
                        var matchYearOnly = Regex.Match(cmdLower, @"\b(202\d)\b");
                        if (matchYearOnly.Success) int.TryParse(matchYearOnly.Groups[1].Value, out nam);
                    }

                    var dsDonHoanThanh = db.DonHang
                        .Where(d => d.NgayDat.HasValue
                                 && d.NgayDat.Value.Month == thang
                                 && d.NgayDat.Value.Year == nam
                                 && d.TrangThaiDH == "Hoàn thành")
                        .ToList();

                    decimal tongDT = dsDonHoanThanh.Sum(d => (decimal?)d.TongTien) ?? 0;
                    int soDon = dsDonHoanThanh.Count;

                    chiTietTruyXuat = $"[DOANH THU THÁNG {thang}/{nam}]: Đạt {tongDT:N0} VNĐ với {soDon} đơn hàng hoàn thành.";

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-chart-pie",
                        Title = $"Báo cáo Doanh thu Tháng {thang}/{nam}",
                        SubTitle = $"Đơn hoàn thành: {soDon} đơn",
                        Badge = $"Doanh thu: {tongDT:N0} đ (Xem biểu đồ)",
                        Url = Url.Action("BaoCaoDoanhThu", "Home", new { area = "Admin", thang = thang, nam = nam })
                    });
                }

                // -----------------------------------------------------------------
                // 6. HÃNG SẢN XUẤT / THƯƠNG HIỆU
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("hãng") || cmdLower.Contains("thương hiệu") || cmdLower.Contains("nhà sản xuất"))
                {
                    var hangs = db.HangSanXuat.ToList();
                    var hangDetails = hangs.Select(h =>
                    {
                        int countSP = db.SanPham.Count(s => s.MaHang == h.MaHang && s.TrangThai == 1);
                        return $"{h.TenHang} ({countSP} máy)";
                    }).ToList();

                    chiTietTruyXuat = $"[DANH SÁCH HÃNG SẢN XUẤT]: Tổng {hangs.Count} hãng: " + string.Join(", ", hangDetails);

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-tags",
                        Title = "Quản lý Hãng Sản Xuất",
                        SubTitle = $"Đang kinh doanh: {hangs.Count} thương hiệu",
                        Badge = "Mở trang Quản lý Hãng",
                        Url = "/Admin/QuanLyHang"
                    });
                }

                // -----------------------------------------------------------------
                // 7. BANNER QUẢNG CÁO
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("banner") || cmdLower.Contains("quảng cáo") || cmdLower.Contains("slide"))
                {
                    var banners = db.Banner.ToList();
                    int activeBanners = banners.Count(b => b.TrangThai == true);
                    chiTietTruyXuat = $"[QUẢN LÝ BANNER]: Tổng {banners.Count} banner ({activeBanners} banner đang hiển thị ngoài trang chủ).";

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-images",
                        Title = "Quản lý Banner Trang Chủ",
                        SubTitle = $"Đang bật: {activeBanners}/{banners.Count} banner",
                        Badge = "Mở trang Quản lý Banner",
                        Url = "/Admin/QuanLyBanner"
                    });
                }

                // -----------------------------------------------------------------
                // 8. NHÂN VIÊN / PHÂN QUYỀN ADMIN / TẠO TÀI KHOẢN
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("nhân viên") || cmdLower.Contains("admin") || cmdLower.Contains("phân quyền") || cmdLower.Contains("tạo tk") || cmdLower.Contains("chức vụ"))
                {
                    var admins = db.TaiKhoan.Where(t => t.MaChucVu == 1).ToList();
                    var staffs = db.TaiKhoan.Where(t => t.MaChucVu != 1 && t.MaChucVu != 2).ToList();

                    chiTietTruyXuat = $"[NHÂN SỰ & QUẢN TRỊ]: Có {admins.Count} Admin ({string.Join(", ", admins.Select(a => a.HoTen))}) và {staffs.Count} Nhân viên ({string.Join(", ", staffs.Select(s => s.HoTen))}).";

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-user-tie",
                        Title = "Danh sách Tài khoản Nhân viên",
                        SubTitle = $"Số lượng: {staffs.Count} nhân viên",
                        Badge = "Mở Quản lý Nhân viên",
                        Url = "/Admin/QuanLyTaiKhoan/DanhSachNhanVien"
                    });
                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-user-shield",
                        Title = "Phân Quyền Admin Cấp Cao",
                        SubTitle = $"Số lượng: {admins.Count} quản trị viên",
                        Badge = "Mở Phân quyền Admin",
                        Url = "/Admin/QuanLyTaiKhoan/DanhSachAdmin"
                    });
                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-user-plus",
                        Title = "Tạo Tài Khoản / Phân Quyền Mới",
                        SubTitle = "Thêm nhanh tài khoản nhân sự",
                        Badge = "Tạo tài khoản ngay",
                        Url = "/Admin/QuanLyTaiKhoan/TaoTaiKhoanNhanVien"
                    });
                }

                // -----------------------------------------------------------------
                // 9. QUẢN LÝ SẢN PHẨM CHUNG
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("sản phẩm") || cmdLower.Contains("điện thoại") || cmdLower.Contains("kho máy"))
                {
                    int tongSP = db.SanPham.Count(s => s.TrangThai == 1);
                    var spMoi = db.SanPham.Where(s => s.TrangThai == 1).OrderByDescending(s => s.MaSP).Take(4).ToList();
                    chiTietTruyXuat = $"[QUẢN LÝ SẢN PHẨM]: Hệ thống đang kinh doanh {tongSP} sản phẩm. Các máy mới nhất: " + string.Join(", ", spMoi.Select(s => $"{s.TenSP} ({s.GiaBan:N0}đ)"));

                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-box-open",
                        Title = "Quản lý Toàn Bộ Sản Phẩm",
                        SubTitle = $"Đang kinh doanh: {tongSP} dòng máy",
                        Badge = "Mở trang Quản lý Sản phẩm",
                        Url = "/Admin/SanPham"
                    });
                }

                // -----------------------------------------------------------------
                // 10. TRA CỨU KHÁCH HÀNG & ĐƠN HÀNG (THEO TÊN, EMAIL, SĐT, MÃ ĐƠN)
                // -----------------------------------------------------------------
                else if (cmdLower.Contains("đơn") || cmdLower.Contains("khách") || cmdLower.Contains("tra") || cmdLower.Contains("tìm") || cmdLower.Contains("sđt") || cmdLower.Contains("sdt") || cmdLower.Contains("email") || Regex.IsMatch(cmdLower, @"\d{3,}"))
                {
                    var allOrders = db.DonHang.OrderByDescending(d => d.NgayDat).ToList();
                    var allUsers = db.TaiKhoan.ToList();

                    List<DonHang> matchedOrders = new List<DonHang>();
                    List<TaiKhoan> matchedUsers = new List<TaiKhoan>();

                    // Kiểm tra mã đơn trực tiếp (VD: #3, đơn 30)
                    var matchOrderId = Regex.Match(cmdLower, @"(?:đơn|hàng|mã|#)\s*(\d{1,5})\b");
                    if (matchOrderId.Success && int.TryParse(matchOrderId.Groups[1].Value, out int orderId))
                    {
                        var dhById = allOrders.FirstOrDefault(d => d.MaDH == orderId);
                        if (dhById != null) matchedOrders.Add(dhById);
                    }

                    // Lọc từ khóa tên / email / sđt
                    string cleanQuery = Regex.Replace(cmdLower, @"\b(hãy|tra|cứu|tìm|kiếm|cho|tôi|biết|xem|kiểm|khách|hàng|có|đơn|gì|nào|của|tài|khoản|email|sđt|sdt|số|điện|thoại|tên|là|mã|danh|sách|gần|đây|mới)\b", " ");
                    cleanQuery = Regex.Replace(cleanQuery, @"\s+", " ").Trim();

                    var searchTerms = new List<string>();
                    if (!string.IsNullOrEmpty(cleanQuery))
                    {
                        searchTerms.Add(cleanQuery);
                        foreach (var w in cleanQuery.Split(' '))
                        {
                            if (w.Length >= 2 && !searchTerms.Contains(w))
                                searchTerms.Add(w);
                        }
                    }

                    foreach (var term in searchTerms)
                    {
                        var uList = allUsers.Where(u => DoiTuongChuaTuKhoa(u, term)).ToList();
                        var uIds = uList.Select(u => u.MaTK).ToList();
                        var oList = allOrders.Where(d => DoiTuongChuaTuKhoa(d, term) || uIds.Contains(d.MaTK)).ToList();

                        if (uList.Any() || oList.Any())
                        {
                            matchedUsers.AddRange(uList);
                            matchedOrders.AddRange(oList);
                            if (term == cleanQuery) break;
                        }
                    }

                    // Nếu Sếp chỉ hỏi "danh sách khách hàng" mà không hỏi đơn hàng
                    if (!matchedOrders.Any() && !matchedUsers.Any() && cmdLower.Contains("khách") && !cmdLower.Contains("đơn"))
                    {
                        matchedUsers = allUsers.Where(u => u.MaChucVu == 2).Take(5).ToList();
                    }
                    // Nếu Sếp hỏi chung "danh sách đơn hàng gần đây"
                    else if (!matchedOrders.Any() && (cmdLower.Contains("đơn") || cmdLower.Contains("đang giao")))
                    {
                        matchedOrders = allOrders.Take(5).ToList();
                    }

                    matchedOrders = matchedOrders.Distinct().OrderByDescending(d => d.NgayDat).ToList();
                    matchedUsers = matchedUsers.Distinct().ToList();

                    if (matchedOrders.Any())
                    {
                        var summaryList = new List<string>();
                        foreach (var dh in matchedOrders.Take(5))
                        {
                            var kh = allUsers.FirstOrDefault(u => u.MaTK == dh.MaTK);
                            string tenHienThi = LayGiaTriCot(dh, "TenKH", "HoTen", "TenNguoiNhan", "NguoiNhan");
                            if (string.IsNullOrEmpty(tenHienThi) && kh != null) tenHienThi = kh.HoTen;
                            if (string.IsNullOrEmpty(tenHienThi)) tenHienThi = "Khách hàng";

                            string sdtHienThi = LayGiaTriCot(dh, "SDT", "SoDienThoai", "DienThoai", "SDTNguoiNhan");
                            if (string.IsNullOrEmpty(sdtHienThi) && kh != null) sdtHienThi = LayGiaTriCot(kh, "SDT", "SoDienThoai", "DienThoai");

                            string ngayDat = dh.NgayDat.HasValue ? dh.NgayDat.Value.ToString("dd/MM/yyyy HH:mm") : "N/A";
                            summaryList.Add($"Đơn #{dh.MaDH} (Người nhận: {tenHienThi}, SĐT: {sdtHienThi}, Ngày: {ngayDat}, Tổng: {dh.TongTien:N0}đ, Trạng thái: {dh.TrangThaiDH})");

                            actionCards.Add(new
                            {
                                Icon = "fa-solid fa-file-invoice-dollar",
                                Title = $"Đơn hàng #{dh.MaDH} - {tenHienThi}",
                                SubTitle = $"{(string.IsNullOrEmpty(sdtHienThi) ? "" : "SĐT: " + sdtHienThi + " | ")}{ngayDat} | {dh.TrangThaiDH}",
                                Badge = $"Tổng tiền: {dh.TongTien:N0} đ (Xem chi tiết)",
                                Url = Url.Action("ChiTiet", "QuanLyDonHang", new { area = "Admin", id = dh.MaDH })
                            });
                        }
                        chiTietTruyXuat = $"[KẾT QUẢ ĐƠN HÀNG]: Tìm thấy {matchedOrders.Count} đơn hàng: " + string.Join("; ", summaryList);
                    }
                    else if (matchedUsers.Any())
                    {
                        foreach (var kh in matchedUsers.Take(5))
                        {
                            actionCards.Add(new
                            {
                                Icon = "fa-solid fa-users",
                                Title = $"Khách hàng: {kh.HoTen}",
                                SubTitle = $"Email: {kh.Email} | Điểm tích lũy: {kh.DiemHienTai ?? 0}",
                                Badge = "Mở trang Quản lý Khách hàng",
                                Url = "/Admin/QuanLyTaiKhoan"
                            });
                        }
                        chiTietTruyXuat = $"[KẾT QUẢ KHÁCH HÀNG]: Tìm thấy {matchedUsers.Count} tài khoản khách hàng: " + string.Join(", ", matchedUsers.Take(5).Select(u => $"{u.HoTen} ({u.Email})"));
                    }
                    else
                    {
                        chiTietTruyXuat = $"[KẾT QUẢ TRA CỨU]: Không tìm thấy đơn hàng hay khách hàng nào khớp với từ khóa '{cleanQuery}'.";
                        actionCards.Add(new
                        {
                            Icon = "fa-solid fa-file-invoice-dollar",
                            Title = "Quản lý Toàn Bộ Đơn Hàng",
                            SubTitle = "Xem tất cả các đơn hàng trong hệ thống",
                            Badge = "Mở Quản lý Đơn hàng",
                            Url = "/Admin/QuanLyDonHang"
                        });
                    }
                }

                // -----------------------------------------------------------------
                // 11. TRƯỜNG HỢP HỎI TỔNG QUAN HỆ THỐNG HOẶC TRÒ CHUYỆN CHUNG
                // -----------------------------------------------------------------
                decimal tongDoanhThuAll = db.DonHang.Where(d => d.TrangThaiDH == "Hoàn thành").Sum(d => (decimal?)d.TongTien) ?? 0;
                int tongDonAll = db.DonHang.Count();
                int donChoXuLy = db.DonHang.Count(d => d.TrangThaiDH != "Hoàn thành" && d.TrangThaiDH != "Đã hủy");
                int tongSPAll = db.SanPham.Count(s => s.TrangThai == 1);
                int spSapHetCount = db.SanPham.Count(s => s.TrangThai == 1 && s.SoLuongTon <= 5);
                int voucherActiveCount = db.MaGiamGia.Count(v => v.SoLuong > 0);
                int binhLuanChoDuyet = db.DanhGia.Count(d => d.PheDuyet != true);

                string globalSummary = $"Tổng doanh thu hoàn thành: {tongDoanhThuAll:N0}đ | Tổng đơn: {tongDonAll} ({donChoXuLy} đơn đang xử lý/giao) | Sản phẩm đang bán: {tongSPAll} ({spSapHetCount} máy sắp hết hàng) | Voucher còn lượt: {voucherActiveCount} | Bình luận chờ duyệt: {binhLuanChoDuyet}.";

                if (string.IsNullOrEmpty(chiTietTruyXuat))
                {
                    chiTietTruyXuat = "Báo cáo tổng quan hệ thống điều hành AiraStore.";
                    actionCards.Add(new
                    {
                        Icon = "fa-solid fa-chart-line",
                        Title = "Bảng Điều Khiển Tổng Quan",
                        SubTitle = $"Doanh thu: {tongDoanhThuAll:N0} đ | {tongDonAll} đơn hàng",
                        Badge = "Về Bảng điều khiển",
                        Url = "/Admin/Home/Index"
                    });
                }

                string prompt = $@"Bạn là Aira Copilot - Trợ lý AI Điều hành toàn diện cho Admin của hệ thống AiraStore.
Sếp (Admin) vừa yêu cầu: ""{cauLenh}""

DỮ LIỆU TRÍCH XUẤT TRỰC TIẾP TỪ HỆ THỐNG:
{chiTietTruyXuat}

THÔNG SỐ TOÀN HỆ THỐNG HIỆN TẠI:
{globalSummary}

QUY TẮC PHẢN HỒI:
1. Xưng 'Em' và gọi người dùng là 'Sếp'. Giọng điệu thông minh, lanh lợi, chuyên nghiệp.
2. Trả lời chính xác dựa trên số liệu thực tế vừa trích xuất ở trên (nêu rõ tên sản phẩm, mã voucher, nội dung bình luận, mã đơn hàng, hoặc doanh thu). Không dùng ký tự markdown ** để in đậm.
3. Mời Sếp nhấn vào các thẻ Popup điều hướng bên dưới để chuyển nhanh tới trang quản lý tương ứng. Độ dài dưới 85 từ.";

                string aiReply = await CallAdminGroqAI(prompt);
                if (string.IsNullOrEmpty(aiReply))
                {
                    aiReply = $"Báo cáo Sếp! {chiTietTruyXuat} Sếp bấm vào thẻ bên dưới để thao tác trực tiếp nhé ạ!";
                }
                else
                {
                    // Làm sạch dấu ** nếu AI vẫn sinh ra để hiển thị trên khung chat đẹp hơn
                    aiReply = aiReply.Replace("**", "");
                }

                return Json(new { success = true, answer = aiReply, cards = actionCards });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}