using Project_Cellphoness.Models;
using System.Linq;
using System.Web.Mvc;

namespace Project_Cellphoness.Areas.Admin.Controllers
{
    public class QuanLyMaGiamGiaController : BaseAdminController
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        public ActionResult Index()
        {
            var lstVoucher = db.MaGiamGia.OrderByDescending(v => v.NgayBatDau).ToList();
            return View(lstVoucher);
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(MaGiamGia vc)
        {
            if (ModelState.IsValid)
            {
                vc.MaVoucher = vc.MaVoucher.Trim().ToUpper();

                var checkTrang = db.MaGiamGia.SingleOrDefault(v => v.MaVoucher == vc.MaVoucher);
                if (checkTrang != null)
                {
                    ViewBag.ThongBao = "Mã Voucher này đã tồn tại trong hệ thống!";
                    return View(vc);
                }

                // QĐ22: Validate ngày và hạn mức mã giảm giá
                if (vc.PhanTramGiam > 50)
                {
                    ViewBag.ThongBao = "Quy định QĐ22: Mức giảm giá không được vượt quá 50%!";
                    return View(vc);
                }

                if (vc.NgayKetThuc <= vc.NgayBatDau)
                {
                    ViewBag.ThongBao = "Quy định QĐ22: Ngày kết thúc phải lớn hơn ngày bắt đầu!";
                    return View(vc);
                }

                // ==========================================
                // LOGIC BIG UPDATE: VALIDATE ĐIỂM ĐỔI THƯỞNG
                // ==========================================
                if (vc.HienThiDoiThuong == true && (vc.DiemYeuCau == null || vc.DiemYeuCau < 0))
                {
                    ViewBag.ThongBao = "Lỗi: Khi chọn Hiển thị Đổi thưởng, Điểm yêu cầu không được để trống và phải >= 0!";
                    return View(vc);
                }

                // Đảm bảo không bị null nếu admin không tick
                if (vc.HienThiDoiThuong == null) vc.HienThiDoiThuong = false;
                if (vc.DiemYeuCau == null) vc.DiemYeuCau = 0;
                // ==========================================

                db.MaGiamGia.Add(vc);
                db.SaveChanges();

                TempData["ThongBao"] = "Tạo mã giảm giá thành công!";
                return RedirectToAction("Index");
            }
            return View(vc);
        }

        public ActionResult Delete(string id)
        {
            var vc = db.MaGiamGia.SingleOrDefault(v => v.MaVoucher == id);
            if (vc != null)
            {
                try
                {
                    db.MaGiamGia.Remove(vc);
                    db.SaveChanges();
                    TempData["ThongBao"] = "Xóa mã giảm giá thành công!";
                }
                catch
                {
                    TempData["LoiXoa"] = "Không thể xóa mã này vì đã có đơn hàng sử dụng! Gợi ý: Hãy sửa số lượng về 0.";
                }
            }
            return RedirectToAction("Index");
        }
    }
}