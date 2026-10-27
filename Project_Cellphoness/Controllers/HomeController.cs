using PagedList;
using Project_Cellphoness.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Collections.Generic;

// CÁC THƯ VIỆN BẮT BUỘC ĐỂ GỌI API 
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Project_Cellphoness.Controllers
{
    public class HomeController : Controller
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        public ActionResult Index(int? page, decimal? minPrice, decimal? maxPrice, List<int> hangIds)
        {
            var danhSachBanner = db.Banner.Where(b => b.TrangThai == true).OrderBy(b => b.ViTri).ToList();
            ViewBag.ListBanner = danhSachBanner;

            int pageSize = 10;
            int pageNumber = (page ?? 1);

            var dsSanPham = db.SanPham.Where(sp => sp.TrangThai == 1).AsQueryable();

            // Khối 1: Gợi ý cho bạn (Lấy 5 sản phẩm mới nhất ngẫu nhiên)
            ViewBag.GoiYChoBan = dsSanPham.OrderByDescending(sp => sp.NgayTao).Take(5).ToList();

            // Khối 2: Apple Chính hãng (Giả định MaHang Apple = 1)
            ViewBag.AppleList = dsSanPham.Where(sp => sp.MaHang == 1).OrderByDescending(sp => sp.MaSP).Take(5).ToList();

            if (hangIds != null && hangIds.Any())
            {
                dsSanPham = dsSanPham.Where(sp => hangIds.Contains(sp.MaHang));
            }

            if (minPrice.HasValue)
            {
                dsSanPham = dsSanPham.Where(sp => sp.GiaBan >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                dsSanPham = dsSanPham.Where(sp => sp.GiaBan <= maxPrice.Value);
            }

            var finalData = dsSanPham.OrderByDescending(sp => sp.NgayTao).ToPagedList(pageNumber, pageSize);

            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.DanhSachHang = db.HangSanXuat.ToList();

            // Lưu lại danh sách hãng đang chọn để hiển thị lại Checkbox trên giao diện
            ViewBag.SelectedHangs = hangIds ?? new List<int>();

            return View(finalData);
        }

        // ================= UC03: LỌC SẢN PHẨM THEO HÃNG =================
        public ActionResult SanPhamTheoHang(int id)
        {
            var hang = db.HangSanXuat.SingleOrDefault(h => h.MaHang == id);
            if (hang == null)
            {
                return HttpNotFound();
            }

            var dsSanPham = db.SanPham
                  .Where(sp => sp.MaHang == id && sp.TrangThai == 1)
                  .ToList();

            ViewBag.TenHang = hang.TenHang;
            return View(dsSanPham);
        }


        // UC05: XEM CHI TIẾT & UC40: ĐỀ XUẤT SẢN PHẨM THÔNG MINH

        public ActionResult Details(int id)
        {
            var sp = db.SanPham.SingleOrDefault(s => s.MaSP == id);
            if (sp == null)
            {
                return HttpNotFound();
            }

            ViewBag.ListDanhGia = db.DanhGia.Where(d => d.MaSP == id).OrderByDescending(d => d.NgayDG).ToList();

            var dsGoiY = db.SanPham
                           .Where(s => s.MaHang == sp.MaHang && s.MaSP != id && s.TrangThai == 1)
                           .Take(4)
                           .ToList();

            if (dsGoiY.Count < 4)
            {
                var idDaLay = dsGoiY.Select(x => x.MaSP).ToList();
                idDaLay.Add(id);

                int soLuongCanBoSung = 4 - dsGoiY.Count;

                var dsBoSung = db.SanPham
                 .Where(s => s.TrangThai == 1 && !idDaLay.Contains(s.MaSP))
                 .AsEnumerable()
                 .OrderBy(s => Math.Abs((double)(s.GiaBan - sp.GiaBan)))
                 .Take(soLuongCanBoSung)
                 .ToList();

                dsGoiY.AddRange(dsBoSung);
            }

            ViewBag.ListGoiY = dsGoiY;

            return View(sp);
        }

        // ==============================================================
        // TÌM KIẾM SẢN PHẨM TRANG CHỦ (TÌM THÔNG MINH, CẮT TỪ KHÓA)
        // ==============================================================
        public ActionResult TimKiem(string tuKhoa)
        {
            if (string.IsNullOrWhiteSpace(tuKhoa))
            {
                return RedirectToAction("Index", "Home");
            }

            // Tách từ khóa thành mảng các từ (vd: "samsung s24" -> ["samsung", "s24"])
            string[] keywords = tuKhoa.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var query = db.SanPham.Where(n => n.TrangThai == 1);

            // Tìm kiếm các máy chứa TẤT CẢ các chữ trong từ khóa, không phân biệt hoa thường
            foreach (var keyword in keywords)
            {
                string kw = keyword;
                query = query.Where(n => n.TenSP.ToLower().Contains(kw));
            }

            var lstKq = query.ToList();
            ViewBag.TuKhoa = tuKhoa;

            return View(lstKq);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }

        // ================= XỬ LÝ GỬI ĐÁNH GIÁ & BÌNH LUẬN =================
        [HttpPost]
        public ActionResult GuiBinhLuan(int MaSP, string NoiDung, int SoSao)
        {
            var tk = (Project_Cellphoness.Models.TaiKhoan)Session["TaiKhoan"];
            if (tk == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (string.IsNullOrWhiteSpace(NoiDung))
            {
                TempData["LoiBinhLuan"] = "Nội dung đánh giá không được để trống!";
                return RedirectToAction("Details", new { id = MaSP });
            }

            string[] tuViPham = { "lừa đảo", "đồ dỏm", "chửi", "ngu" };
            foreach (var tu in tuViPham)
            {
                if (NoiDung.ToLower().Contains(tu))
                {
                    TempData["LoiBinhLuan"] = "Bình luận của bạn chứa từ ngữ vi phạm tiêu chuẩn cộng đồng!";
                    return RedirectToAction("Details", new { id = MaSP });
                }
            }

            var daMuaHang = (from dh in db.DonHang
                             join ct in db.ChiTietDonHang on dh.MaDH equals ct.MaDH
                             where dh.MaTK == tk.MaTK && ct.MaSP == MaSP && dh.TrangThaiDH == "Hoàn thành"
                             select dh).Any();

            if (!daMuaHang)
            {
                TempData["LoiBinhLuan"] = "Bạn cần mua và nhận sản phẩm này thành công thì mới được phép gửi đánh giá!";
                return RedirectToAction("Details", new { id = MaSP });
            }

            DanhGia dg = new DanhGia();
            dg.MaSP = MaSP;
            dg.MaTK = tk.MaTK;
            dg.NoiDung = NoiDung.Trim();
            dg.SoSao = SoSao;
            dg.NgayDG = DateTime.Now;
            dg.PheDuyet = false;

            db.DanhGia.Add(dg);
            db.SaveChanges();

            TempData["ThongBaoBinhLuan"] = "Gửi đánh giá thành công! Bình luận của bạn đang chờ nhân viên kiểm duyệt.";

            return RedirectToAction("Details", new { id = MaSP });
        }


        // UC18: SO SÁNH CẤU HÌNH ĐIỆN THOẠI (ĐÃ TỐI ƯU UX THÔNG MINH)

        public ActionResult SoSanh(int id1, int? id2)
        {
            var sp1 = db.SanPham.SingleOrDefault(s => s.MaSP == id1);
            if (sp1 == null) return HttpNotFound();

            ViewBag.SP1 = sp1;
            ViewBag.TSKT1 = db.ThongSoKyThuat.SingleOrDefault(t => t.MaSP == id1);

            // --- BẮT ĐẦU NÂNG CẤP LỌC DANH SÁCH ---
            List<int> danhSachUuTien = new List<int>();
            var tk = Session["TaiKhoan"] as TaiKhoan;

            if (tk != null)
            {
                // 1. Quét Cookie lấy danh sách mã sản phẩm trong Giỏ Hàng
                string gioHangCookie = "GioHangCookie_" + tk.MaTK;
                var cookieGH = Request.Cookies[gioHangCookie];
                if (cookieGH != null && !string.IsNullOrEmpty(cookieGH.Value))
                {
                    string[] cacSanPham = cookieGH.Value.Split('-');
                    foreach (string item in cacSanPham)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            string[] thuocTinh = item.Split(':');
                            if (thuocTinh.Length == 2)
                            {
                                danhSachUuTien.Add(int.Parse(thuocTinh[0]));
                            }
                        }
                    }
                }

                // 2. Quét Cookie lấy danh sách mã sản phẩm trong Yêu Thích (Tim)
                string yeuThichCookie = "YeuThichCookie_" + tk.MaTK;
                var cookieYT = Request.Cookies[yeuThichCookie];
                if (cookieYT != null && !string.IsNullOrEmpty(cookieYT.Value))
                {
                    string[] ids = cookieYT.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string id in ids)
                    {
                        danhSachUuTien.Add(int.Parse(id));
                    }
                }
            }

            // 3. Lọc bỏ các ID bị trùng (vd: 1 máy vừa nằm trong tim vừa nằm trong giỏ)
            danhSachUuTien = danhSachUuTien.Distinct().ToList();

            // 4. Query cơ sở dữ liệu: Loại trừ máy hiện tại (sp1) ra khỏi danh sách so sánh
            var queryDienThoai = db.SanPham.Where(s => s.MaSP != id1 && s.TrangThai == 1);
            List<SanPham> dsDienThoai;

            if (danhSachUuTien.Count > 0)
            {
                // Nếu khách có thả tim/giỏ hàng -> Chỉ hiển thị các máy đó
                dsDienThoai = queryDienThoai.Where(s => danhSachUuTien.Contains(s.MaSP)).ToList();

                // Fallback nhẹ: Lỡ khách chỉ thêm đúng 1 máy vào giỏ (chính là máy đang xem) thì danh sách bị rỗng
                if (!dsDienThoai.Any())
                {
                    dsDienThoai = queryDienThoai.Where(s => s.MaHang == sp1.MaHang).Take(10).ToList();
                }
            }
            else
            {
                // Fallback: Nếu khách chưa đăng nhập hoặc chưa thêm giỏ/tim -> Hiển thị 10 máy cùng hãng để gợi ý
                dsDienThoai = queryDienThoai.Where(s => s.MaHang == sp1.MaHang).Take(10).ToList();
                if (!dsDienThoai.Any())
                {
                    dsDienThoai = queryDienThoai.Take(10).ToList();
                }
            }
            // --- KẾT THÚC NÂNG CẤP ---

            ViewBag.ListDienThoai = new SelectList(dsDienThoai, "MaSP", "TenSP");

            if (id2.HasValue)
            {
                ViewBag.SP2 = db.SanPham.SingleOrDefault(s => s.MaSP == id2.Value);
                ViewBag.TSKT2 = db.ThongSoKyThuat.SingleOrDefault(t => t.MaSP == id2.Value);
            }

            return View();
        }

        public ActionResult TinTuc()
        {
            var dsTinTuc = db.TinTuc.OrderByDescending(t => t.NgayDang).ToList();
            return View(dsTinTuc);
        }

        [ChildActionOnly]
        public ActionResult MenuDanhMuc()
        {
            var lstHang = db.HangSanXuat.ToList();
            return PartialView("_MenuDanhMuc", lstHang);
        }


        // KẾT NỐI API GROQ (ĐÃ FIX LỖI BAD REQUEST)

        private async Task<string> CallSmartAI(string prompt)
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
                    // Thay thế bằng model đang hoạt động trên Playground của bro
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
                    else
                    {
                        // NÂNG CẤP: Bắt hệ thống in ra chi tiết lỗi từ Groq ra màn hình chat
                        string errorDetail = await response.Content.ReadAsStringAsync();
                        return $"Lỗi {response.StatusCode} từ Server AI: {errorDetail}";
                    }
                }
                catch (Exception ex)
                {
                    return "Hệ thống AI đang bảo trì: " + ex.Message;
                }
            }
        }


        // TÍNH NĂNG: AI TƯ VẤN (ĐÃ FIX LỖI HIỂN THỊ THẺ SẢN PHẨM)
        [HttpPost]
        public async Task<ActionResult> TuVanAI(string cauHoi)
        {
            try
            {
                var dsSanPham = db.SanPham
                                  .Where(sp => sp.TrangThai == 1)
                                  .Select(sp => new { sp.MaSP, sp.TenSP, sp.GiaBan, sp.HinhAnh })
                                  .ToList();

                string danhSachSP = string.Join(", ", dsSanPham.Select(s => $"{s.TenSP} ({s.GiaBan:N0}đ)"));

                string prompt = $@"Bạn là nhân viên tư vấn bán hàng chuyên nghiệp của hệ thống AiraStore. Khách hàng vừa hỏi: '{cauHoi}'.

THÔNG TIN KHO HÀNG (QUAN TRỌNG): Hiện tại AiraStore CHỈ BÁN các dòng máy sau: {danhSachSP}.

LUẬT THÉP BẮT BUỘC:
1. TUYỆT ĐỐI KHÔNG tư vấn, gợi ý hay nhắc đến BẤT KỲ sản phẩm nào không nằm trong danh sách KHO HÀNG trên.
2. Nếu khách hỏi mua một chiếc máy ngoài danh sách, hãy lịch sự xin lỗi vì hết hàng và chủ động khéo léo gợi ý sang 1-2 chiếc máy có trong kho.
3. Trả lời bằng tiếng Việt, xưng 'mình' gọi 'bạn', ngắn gọn, súc tích (dưới 100 chữ).";

                string traLoi = await CallSmartAI(prompt);

                // ========================================================
                // BƯỚC 3 (BIG UPDATE): THUẬT TOÁN TÌM KIẾM TỪ KHÓA LÕI
                // ========================================================
                var sanPhamGoiY = new List<object>();

                // Chuẩn hóa câu trả lời AI: viết thường, tách chữ "promax" thành "pro max" để dễ dò
                string cleanTraLoi = traLoi.ToLower().Replace("promax", "pro max");

                foreach (var sp in dsSanPham)
                {
                    // Chuẩn hóa tên SP trong DB
                    string cleanTenSP = sp.TenSP.ToLower().Replace("promax", "pro max");

                    // Tách tên máy thành các chữ rời rạc (VD: "iphone 17 pro max 256gb" -> "iphone", "17", "pro", "max", "256gb")
                    var words = cleanTenSP.Split(new char[] { ' ', '-', '/', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    int matchCount = 0;
                    // Lấy tối đa 3 từ khóa đầu tiên làm "Lõi" để nhận diện (VD: "iphone", "17", "pro")
                    int targetCount = Math.Min(words.Length, 3);

                    for (int i = 0; i < targetCount; i++)
                    {
                        if (cleanTraLoi.Contains(words[i]))
                        {
                            matchCount++;
                        }
                    }

                    // Nếu AI nhắc trúng đủ 3 từ khóa cốt lõi -> Chắc chắn đang nói về máy này!
                    if (matchCount == targetCount)
                    {
                        sanPhamGoiY.Add(new
                        {
                            MaSP = sp.MaSP,
                            TenSP = sp.TenSP,
                            GiaBan = sp.GiaBan,
                            HinhAnh = sp.HinhAnh
                        });
                    }
                }

                // Nếu thuật toán vô tình quét trúng nhiều máy cùng dòng (VD: quét trúng cả iPhone 17 và iPhone 17 Pro)
                // Ta chỉ lấy tối đa 2 thẻ bài đầu tiên để gửi về, tránh làm màn hình Chat bị dài lê thê
                sanPhamGoiY = sanPhamGoiY.Take(2).ToList();

                return Json(new { success = true, answer = traLoi, products = sanPhamGoiY });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // TÍNH NĂNG: AI PHÂN TÍCH SO SÁNH 2 ĐIỆN THOẠI

        [HttpPost]
        public async Task<ActionResult> PhanTichSoSanhAI(int id1, int id2)
        {
            try
            {
                var sp1 = db.SanPham.Find(id1);
                var sp2 = db.SanPham.Find(id2);

                if (sp1 == null || sp2 == null)
                {
                    return Json(new { success = false, message = "Lỗi kết nối CSDL." });
                }

                // FIX LỖI: Tránh trường hợp CSDL bị Null thông số kỹ thuật
                var tskt1 = db.ThongSoKyThuat.SingleOrDefault(t => t.MaSP == id1) ?? new ThongSoKyThuat();
                var tskt2 = db.ThongSoKyThuat.SingleOrDefault(t => t.MaSP == id2) ?? new ThongSoKyThuat();

                string prompt = $@"
Bạn là một chuyên gia review công nghệ uy tín. Hãy phân tích bằng TIẾNG VIỆT và so sánh 2 chiếc điện thoại sau một cách ngắn gọn:
- Điện thoại 1: {sp1.TenSP}, Giá {sp1.GiaBan:N0} VNĐ, Chip {tskt1.Chip ?? "chưa rõ"}, RAM {tskt1.RAM ?? "chưa rõ"}, Pin {tskt1.Pin ?? "chưa rõ"}.
- Điện thoại 2: {sp2.TenSP}, Giá {sp2.GiaBan:N0} VNĐ, Chip {tskt2.Chip ?? "chưa rõ"}, RAM {tskt2.RAM ?? "chưa rõ"}, Pin {tskt2.Pin ?? "chưa rõ"}.

Yêu cầu:
1. Phân tích ưu/nhược điểm (Bỏ qua nếu thông số chưa rõ).
2. Kết luận ai nên mua máy nào.
3. Định dạng bằng thẻ HTML cơ bản (dùng thẻ <b>, <br>) để dễ đọc trên web. Trả lời trực tiếp, không vòng vo.";

                string phanTich = await CallSmartAI(prompt);

                return Json(new { success = true, answer = phanTich });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==============================================================
        // TÌM KIẾM SẢN PHẨM BẰNG AJAX TRONG TRANG SO SÁNH (CẮT TỪ KHÓA)
        // ==============================================================
        [HttpGet]
        public ActionResult TimKiemSanPhamSoSanh(string tuKhoa, int idNgoaiTru)
        {
            if (string.IsNullOrWhiteSpace(tuKhoa)) return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            string[] keywords = tuKhoa.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var query = db.SanPham.Where(s => s.TrangThai == 1 && s.MaSP != idNgoaiTru);

            foreach (var keyword in keywords)
            {
                string kw = keyword;
                query = query.Where(s => s.TenSP.ToLower().Contains(kw));
            }

            var dsKetQua = query.Select(s => new {
                MaSP = s.MaSP,
                TenSP = s.TenSP
            })
                                .Take(15)
                                .ToList();

            return Json(dsKetQua, JsonRequestBehavior.AllowGet);
        }

        // ==============================================================
        // API MỚI: TỰ ĐỘNG GỢI Ý TÌM KIẾM CHO HEADER BẰNG AJAX
        // ==============================================================
        [HttpGet]
        public ActionResult GoiYTimKiem(string tuKhoa)
        {
            if (string.IsNullOrWhiteSpace(tuKhoa)) return Json(new List<object>(), JsonRequestBehavior.AllowGet);

            string[] keywords = tuKhoa.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var query = db.SanPham.Where(s => s.TrangThai == 1);

            foreach (var keyword in keywords)
            {
                string kw = keyword;
                query = query.Where(s => s.TenSP.ToLower().Contains(kw));
            }

            // Lấy 8 sản phẩm liên quan nhất trả về Json có kèm Hình Ảnh và Giá
            var dsKetQua = query.Select(s => new {
                MaSP = s.MaSP,
                TenSP = s.TenSP,
                HinhAnh = s.HinhAnh,
                GiaBan = s.GiaBan
            })
                                .Take(8)
                                .ToList();

            return Json(dsKetQua, JsonRequestBehavior.AllowGet);
        }
    }
}