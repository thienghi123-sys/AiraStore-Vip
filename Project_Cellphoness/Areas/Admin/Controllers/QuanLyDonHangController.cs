using Project_Cellphoness.Helpers; // KHAI BÁO THÊM HELPER ĐỂ TÍNH ĐIỂM
using Project_Cellphoness.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Project_Cellphoness.Areas.Admin.Controllers
{
    public class QuanLyDonHangController : BaseAdminController
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        // ==============================================================
        // HÀM HỖ TRỢ: CHUYỂN CHUỖI CÓ DẤU THÀNH KHÔNG DẤU & CHỮ THƯỜNG
        // Giúp tìm "khoa", "KHOA", "dang khoa" đều ra "Nguyễn Trần Đăng Khoa", "Trà Khoa"...
        // ==============================================================
        private string ChuanHoaTimKiem(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string str = input.Trim().ToLower();
            string normalized = str.Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (char c in normalized)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace('đ', 'd');
        }

        // ==============================================================
        // DANH SÁCH ĐƠN HÀNG (ĐÃ NÂNG CẤP TÌM KIẾM LINH HOẠT MỌI TỪ KHÓA)
        // ==============================================================
        public ActionResult Index(string tuKhoa, DateTime? tuNgay, DateTime? denNgay, string sapXep, string trangThai)
        {
            // Eager load bảng TaiKhoan để lấy đầy đủ HoTen, SoDienThoai, Email
            var query = db.DonHang.Include("TaiKhoan").AsQueryable();

            // 1. Lọc theo khoảng ngày đặt trên SQL trước để tối ưu tốc độ
            if (tuNgay.HasValue)
            {
                DateTime startDate = tuNgay.Value.Date;
                query = query.Where(n => n.NgayDat >= startDate);
            }

            if (denNgay.HasValue)
            {
                DateTime endDate = denNgay.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(n => n.NgayDat <= endDate);
            }

            // 2. Lọc nhanh theo Trạng thái đơn hàng
            if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "all")
            {
                if (trangThai == "Chờ duyệt")
                {
                    query = query.Where(n => n.TrangThaiDH == null || n.TrangThaiDH == "" || n.TrangThaiDH == "Chờ duyệt");
                }
                else if (trangThai == "Đã hủy")
                {
                    query = query.Where(n => n.TrangThaiDH != null && n.TrangThaiDH.Contains("Đã hủy"));
                }
                else
                {
                    query = query.Where(n => n.TrangThaiDH == trangThai);
                }
            }

            // Đưa về danh sách trên bộ nhớ để xử lý tìm kiếm tiếng Việt linh hoạt 100%
            var danhSach = query.ToList();

            // 3. TÌM KIẾM THÔNG MINH: Không phân biệt HOA/thường, có dấu/không dấu, tách từng từ
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                // Tách từ khóa thành từng chữ rời (VD: "đăng khoa" -> ["dang", "khoa"])
                string[] cacTuKhoa = ChuanHoaTimKiem(tuKhoa)
                    .Replace("#", "")
                    .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                danhSach = danhSach.Where(n =>
                {
                    // Gom toàn bộ thông tin của đơn hàng thành 1 chuỗi tổng hợp để dò
                    string thongTinGop = string.Join(" ",
                        n.MaDH.ToString(),
                        n.HoTenNhan ?? "",
                        n.SDTNhan ?? "",
                        n.TaiKhoan != null ? (n.TaiKhoan.HoTen ?? "") : "",
                        n.TaiKhoan != null ? (n.TaiKhoan.SoDienThoai ?? "") : "",
                        n.TaiKhoan != null ? (n.TaiKhoan.Email ?? "") : ""
                    );

                    string thongTinKhongDau = ChuanHoaTimKiem(thongTinGop);

                    // Chỉ cần chuỗi thông tin chứa TẤT CẢ các chữ mà Admin gõ vào là hiện lên hết!
                    return cacTuKhoa.All(tu => thongTinKhongDau.Contains(tu));
                }).ToList();
            }

            // 4. Sắp xếp theo Giá tiền hoặc Ngày đặt
            switch (sapXep)
            {
                case "gia_giam":
                    danhSach = danhSach.OrderByDescending(n => n.TongTien).ToList();
                    break;
                case "gia_tang":
                    danhSach = danhSach.OrderBy(n => n.TongTien).ToList();
                    break;
                case "cu_nhat":
                    danhSach = danhSach.OrderBy(n => n.NgayDat).ToList();
                    break;
                default:
                    danhSach = danhSach.OrderByDescending(n => n.NgayDat).ToList();
                    break;
            }

            ViewBag.TuKhoa = tuKhoa;
            ViewBag.TuNgay = tuNgay.HasValue ? tuNgay.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.DenNgay = denNgay.HasValue ? denNgay.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.SapXep = sapXep ?? "moi_nhat";
            ViewBag.TrangThai = trangThai ?? "all";

            ViewBag.TongSoDon = danhSach.Count;
            ViewBag.TongDoanhThuLoc = danhSach
                .Where(d => d.TrangThaiDH == null || !d.TrangThaiDH.Contains("Đã hủy"))
                .Sum(d => (decimal?)d.TongTien) ?? 0;

            return View(danhSach);
        }

        public ActionResult ChiTiet(int id)
        {
            var donHang = db.DonHang.SingleOrDefault(n => n.MaDH == id);
            if (donHang == null)
            {
                return HttpNotFound();
            }

            var lstChiTiet = db.ChiTietDonHang.Where(n => n.MaDH == id).ToList();
            ViewBag.DanhSachChiTiet = lstChiTiet;

            // Truy vấn lấy tên nhân viên đã duyệt đơn hàng này
            if (donHang.MaNVDuyet != null)
            {
                var nhanVien = db.TaiKhoan.SingleOrDefault(t => t.MaTK == donHang.MaNVDuyet);
                if (nhanVien != null)
                {
                    ViewBag.TenNhanVien = nhanVien.HoTen;
                }
                else
                {
                    ViewBag.TenNhanVien = "Không xác định";
                }
            }
            else
            {
                ViewBag.TenNhanVien = "Chưa xử lý (Đơn mới)";
            }

            return View(donHang);
        }

        // TRẠNG THÁI ĐƠN HÀNG và QĐ18 (LƯU LỊCH SỬ DUYỆT ĐƠN) & TÍNH ĐIỂM THƯỞNG
        [HttpPost]
        public ActionResult CapNhatTrangThai(int MaDH, string TrangThaiMoi)
        {
            var dh = db.DonHang.SingleOrDefault(n => n.MaDH == MaDH);
            if (dh != null)
            {
                // 1. Cập nhật trạng thái mới do Admin/Nhân viên chọn
                dh.TrangThaiDH = TrangThaiMoi;

                // 2. QĐ18: Lấy thông tin người đang thao tác và lưu vào cột MaNVDuyet
                var admin = Session["TaiKhoan"] as TaiKhoan;
                if (admin != null)
                {
                    dh.MaNVDuyet = admin.MaTK;
                }

                // 3. LOGIC BIG UPDATE: CỘNG ĐIỂM KHI ĐƠN HÀNG HOÀN THÀNH
                if (TrangThaiMoi == "Hoàn thành" && dh.DaCongDiem != true)
                {
                    var khachHang = db.TaiKhoan.SingleOrDefault(t => t.MaTK == dh.MaTK);
                    if (khachHang != null)
                    {
                        decimal tongTien = dh.TongTien;
                        int diemThuong = DiemThuongHelper.TinhDiemTuHoaDon(tongTien);

                        khachHang.DiemTichLuy = (khachHang.DiemTichLuy ?? 0) + diemThuong;
                        khachHang.DiemHienTai = (khachHang.DiemHienTai ?? 0) + diemThuong;

                        dh.DaCongDiem = true;
                    }
                }

                db.SaveChanges();
                TempData["ThongBao"] = "Cập nhật trạng thái và ghi nhận lịch sử thao tác thành công!";
            }

            return RedirectToAction("ChiTiet", new { id = MaDH });
        }

        // UC26: IN HÓA ĐƠN CHI TIẾT (XUẤT PDF / MÁY IN)
        public ActionResult InHoaDon(int id)
        {
            var dh = db.DonHang.SingleOrDefault(n => n.MaDH == id);
            if (dh == null)
            {
                return HttpNotFound();
            }
            var chiTiet = db.ChiTietDonHang.Where(c => c.MaDH == id).ToList();
            ViewBag.ChiTiet = chiTiet;

            return View(dh);
        }
    }
}