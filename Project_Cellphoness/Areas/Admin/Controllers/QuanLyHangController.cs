using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Project_Cellphoness.Models;

namespace Project_Cellphoness.Areas.Admin.Controllers
{
    public class QuanLyHangController : BaseAdminController
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        public ActionResult Index()
        {
            var lstHang = db.HangSanXuat.OrderByDescending(h => h.MaHang).ToList();
            return View(lstHang);
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create(HangSanXuat hang, HttpPostedFileBase fileUpload)
        {
            if (ModelState.IsValid)
            {
                // XỬ LÝ UPLOAD ẢNH LOGO
                if (fileUpload != null && fileUpload.ContentLength > 0)
                {
                    var fileName = Path.GetFileName(fileUpload.FileName);
                    var directoryPath = Server.MapPath("~/Content/images/");

                    // KIỂM TRA VÀ TỰ ĐỘNG TẠO THƯ MỤC NẾU CHƯA CÓ
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    var path = Path.Combine(directoryPath, fileName);
                    fileUpload.SaveAs(path);

                    hang.Logo = "~/Content/images/" + fileName;
                }

                // LƯU VÀO DATABASE
                db.HangSanXuat.Add(hang);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(hang);
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var hang = db.HangSanXuat.SingleOrDefault(h => h.MaHang == id);
            if (hang == null) return HttpNotFound();
            return View(hang);
        }

        [HttpPost]
        public ActionResult Edit(HangSanXuat hang, HttpPostedFileBase fileUpload)
        {
            if (ModelState.IsValid)
            {
                // XỬ LÝ UPLOAD ẢNH LOGO
                if (fileUpload != null && fileUpload.ContentLength > 0)
                {
                    var fileName = Path.GetFileName(fileUpload.FileName);
                    var directoryPath = Server.MapPath("~/Content/images/");

                    // KIỂM TRA VÀ TỰ ĐỘNG TẠO THƯ MỤC NẾU CHƯA CÓ
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    var path = Path.Combine(directoryPath, fileName);
                    fileUpload.SaveAs(path);

                    hang.Logo = "~/Content/images/" + fileName;
                }

                // CẬP NHẬT DATABASE
                db.Entry(hang).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(hang);
        }

        public ActionResult Delete(int id)
        {
            var hang = db.HangSanXuat.SingleOrDefault(h => h.MaHang == id);
            if (hang != null)
            {
                try
                {
                    db.HangSanXuat.Remove(hang);
                    db.SaveChanges();
                }
                catch
                {
                    TempData["LoiXoa"] = "Không thể xóa hãng này vì đang có sản phẩm thuộc hãng này trong kho!";
                }
            }
            return RedirectToAction("Index");
        }
    }
}