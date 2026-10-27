using Project_Cellphoness.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace Project_Cellphoness.Controllers
{
    public class DoiThuongController : Controller
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        // 1. GIAO DIỆN HIỂN THỊ CÁC MÃ CÓ THỂ ĐỔI
        public ActionResult Index()
        {
            var lstVoucher = db.MaGiamGia
                .Where(v => v.HienThiDoiThuong == true && v.SoLuong > 0 && v.NgayKetThuc >= DateTime.Now)
                .OrderBy(v => v.DiemYeuCau)
                .ToList();

            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk != null)
            {
                var currentUser = db.TaiKhoan.SingleOrDefault(t => t.MaTK == tk.MaTK);
                ViewBag.DiemHienTai = currentUser.DiemHienTai ?? 0;
            }
            else
            {
                ViewBag.DiemHienTai = -1;
            }

            return View(lstVoucher);
        }

        // 2. LOGIC XỬ LÝ KHI KHÁCH HÀNG BẤM NÚT "ĐỔI MÃ"
        [HttpPost]
        public ActionResult DoiVoucher(string maVoucher)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null)
                return Json(new { success = false, message = "Bạn cần đăng nhập để đổi thưởng!" });

            var userDb = db.TaiKhoan.SingleOrDefault(t => t.MaTK == tk.MaTK);
            var voucher = db.MaGiamGia.SingleOrDefault(v => v.MaVoucher == maVoucher && v.HienThiDoiThuong == true);

            if (voucher == null || voucher.SoLuong <= 0)
                return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết số lượng!" });

            if (voucher.NgayKetThuc < DateTime.Now)
                return Json(new { success = false, message = "Mã giảm giá đã hết hạn!" });

            int diemYeuCau = voucher.DiemYeuCau ?? 0;
            int diemHienTai = userDb.DiemHienTai ?? 0;

            if (diemHienTai < diemYeuCau)
                return Json(new { success = false, message = "Rất tiếc! Bạn không đủ điểm để đổi mã này." });

            // Kiểm tra xem khách đã đổi mã này chưa (nếu muốn 1 người chỉ được đổi 1 lần thì mở comment đoạn dưới)
            // var daDoi = db.ViVoucher.Any(v => v.MaTK == tk.MaTK && v.MaVoucher == maVoucher);
            // if (daDoi) return Json(new { success = false, message = "Bạn đã đổi mã này rồi, hãy kiểm tra Ví Voucher nhé!" });

            // THỰC HIỆN ĐỔI MÃ
            userDb.DiemHienTai = diemHienTai - diemYeuCau;
            voucher.SoLuong = voucher.SoLuong - 1;

            ViVoucher vi = new ViVoucher();
            vi.MaTK = userDb.MaTK;
            vi.MaVoucher = voucher.MaVoucher;
            vi.TrangThaiSD = false;
            vi.NgayDoi = DateTime.Now;

            db.ViVoucher.Add(vi);
            db.SaveChanges();

            return Json(new { success = true, message = "Thành công! Mã đã được cất vào Ví Voucher của bạn." });
        }

        // 3. LOGIC LẤY DANH SÁCH MÃ TRONG VÍ
        public ActionResult ViVoucher()
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Gửi Tên xuống View để hiển thị ở Sidebar
            ViewBag.HoTen = tk.HoTen;

            // Kết hợp (Join) bảng ViVoucher và MaGiamGia để lấy chi tiết
            var lstVi = (from vi in db.ViVoucher
                         join mg in db.MaGiamGia on vi.MaVoucher equals mg.MaVoucher
                         where vi.MaTK == tk.MaTK
                         orderby vi.NgayDoi descending
                         select new ChiTietViVoucher
                         {
                             MaVi = vi.MaVi,
                             MaVoucher = vi.MaVoucher,
                             TrangThaiSD = vi.TrangThaiSD,
                             NgayDoi = vi.NgayDoi,
                             PhanTramGiam = mg.PhanTramGiam,
                             GiaTriToiDa = mg.GiaTriToiDa,
                             DonToiThieu = mg.DonToiThieu,
                             NgayKetThuc = mg.NgayKetThuc
                         }).ToList();

            return View(lstVi);
        }
    }

    // Lớp chứa dữ liệu gộp (ViewModel) giúp View đọc dễ dàng không bị lỗi
    public class ChiTietViVoucher
    {
        public int MaVi { get; set; }
        public string MaVoucher { get; set; }
        public bool? TrangThaiSD { get; set; }
        public DateTime? NgayDoi { get; set; }
        public int? PhanTramGiam { get; set; }
        public decimal? GiaTriToiDa { get; set; }
        public decimal? DonToiThieu { get; set; }
        public DateTime? NgayKetThuc { get; set; }
    }
}