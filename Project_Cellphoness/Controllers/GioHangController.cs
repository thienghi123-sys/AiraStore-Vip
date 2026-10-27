using Project_Cellphoness.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Net.Mail;

namespace Project_Cellphoness.Controllers
{
    public class GioHangController : Controller
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        public List<GioHang> LayGioHang()
        {
            List<GioHang> lstGioHang = new List<GioHang>();
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return lstGioHang;

            string cookieName = "GioHangCookie_" + tk.MaTK;
            HttpCookie gioHangCookie = Request.Cookies[cookieName];

            if (gioHangCookie != null && !string.IsNullOrEmpty(gioHangCookie.Value))
            {
                string[] cacSanPham = gioHangCookie.Value.Split('-');
                foreach (string item in cacSanPham)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        string[] thuocTinh = item.Split(':');
                        if (thuocTinh.Length == 2)
                        {
                            int maSP = int.Parse(thuocTinh[0]);
                            int soLuong = int.Parse(thuocTinh[1]);

                            GioHang gh = new GioHang(maSP);
                            gh.iSoLuong = soLuong;
                            lstGioHang.Add(gh);
                        }
                    }
                }
            }
            return lstGioHang;
        }

        public void LuuGioHangVaoCookie(List<GioHang> lstGioHang)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return;

            string giaTriCookie = "";
            foreach (var item in lstGioHang)
            {
                giaTriCookie += $"{item.iMaSP}:{item.iSoLuong}-";
            }

            string cookieName = "GioHangCookie_" + tk.MaTK;
            HttpCookie gioHangCookie = new HttpCookie(cookieName);
            gioHangCookie.Value = giaTriCookie;
            gioHangCookie.Expires = DateTime.Now.AddDays(30);
            Response.Cookies.Add(gioHangCookie);
        }

        public ActionResult ThemGioHang(int iMaSP, string strURL)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();
            SanPham sp = db.SanPham.SingleOrDefault(n => n.MaSP == iMaSP);

            if (sp == null || sp.SoLuongTon <= 0)
            {
                return Content("<script>alert('Sản phẩm đã hết hàng!'); window.location.href='" + strURL + "';</script>");
            }

            GioHang sanpham = lstGioHang.Find(n => n.iMaSP == iMaSP);
            if (sanpham == null)
            {
                sanpham = new GioHang(iMaSP);
                lstGioHang.Add(sanpham);
            }
            else
            {
                if (sanpham.iSoLuong < sp.SoLuongTon)
                {
                    sanpham.iSoLuong++;
                }
                else
                {
                    return Content("<script>alert('Số lượng trong giỏ đã đạt mức tồn kho tối đa!'); window.location.href='" + strURL + "';</script>");
                }
            }

            LuuGioHangVaoCookie(lstGioHang);
            return Redirect(strURL);
        }

        public ActionResult GioHangPartial()
        {
            int TongSoLuong = 0;
            var tk = Session["TaiKhoan"] as TaiKhoan;

            if (tk != null)
            {
                List<GioHang> lstGioHang = LayGioHang();
                if (lstGioHang != null)
                {
                    TongSoLuong = lstGioHang.Sum(n => n.iSoLuong);
                }
            }

            ViewBag.TongSoLuong = TongSoLuong;
            return PartialView();
        }

        public ActionResult Index()
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();

            if (lstGioHang.Count == 0)
            {
                ViewBag.ThongBao = "Giỏ hàng của bạn đang trống! Hãy chọn cho mình một sản phẩm ưng ý nhé.";
            }
            else
            {
                ViewBag.TongTien = lstGioHang.Sum(n => n.dThanhTien);

                // --- LOGIC MỚI: LẤY DANH SÁCH VOUCHER TRONG VÍ CỦA KHÁCH ---
                // --- LOGIC MỚI: LẤY DANH SÁCH VOUCHER TRONG VÍ CỦA KHÁCH ---
                var lstVi = (from vi in db.ViVoucher
                             join mg in db.MaGiamGia on vi.MaVoucher equals mg.MaVoucher
                             where vi.MaTK == tk.MaTK
                                   && vi.TrangThaiSD == false // Chưa sử dụng
                                   && mg.NgayKetThuc >= DateTime.Now // Chưa hết hạn
                             orderby vi.NgayDoi descending
                             select mg).ToList(); // SỬA Ở ĐÂY: Select nguyên đối tượng mg luôn

                ViewBag.DanhSachVi = lstVi;
            }

            return View(lstGioHang);
        }

        public ActionResult GiamSoLuong(int iMaSP)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();
            GioHang sanpham = lstGioHang.SingleOrDefault(n => n.iMaSP == iMaSP);

            if (sanpham != null)
            {
                if (sanpham.iSoLuong > 1)
                {
                    sanpham.iSoLuong--;
                    LuuGioHangVaoCookie(lstGioHang);
                }
                else
                {
                    lstGioHang.RemoveAll(n => n.iMaSP == iMaSP);

                    if (lstGioHang.Count == 0)
                    {
                        string cookieName = "GioHangCookie_" + tk.MaTK;
                        HttpCookie gioHangCookie = new HttpCookie(cookieName);
                        gioHangCookie.Expires = DateTime.Now.AddDays(-1);
                        Response.Cookies.Add(gioHangCookie);

                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        LuuGioHangVaoCookie(lstGioHang);
                    }
                }
            }
            return RedirectToAction("Index", "GioHang");
        }

        public ActionResult TangSoLuong(int iMaSP)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();
            GioHang sanpham = lstGioHang.SingleOrDefault(n => n.iMaSP == iMaSP);

            if (sanpham != null)
            {
                SanPham sp = db.SanPham.SingleOrDefault(n => n.MaSP == iMaSP);
                if (sp != null && sanpham.iSoLuong < sp.SoLuongTon)
                {
                    sanpham.iSoLuong++;
                    LuuGioHangVaoCookie(lstGioHang);
                }
                else
                {
                    TempData["LoiTonKho"] = $"Sản phẩm '{sp.TenSP}' chỉ còn tối đa {sp.SoLuongTon} máy trong kho!";
                }
            }
            return RedirectToAction("Index", "GioHang");
        }

        public ActionResult XoaGioHang(int iMaSP)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();
            GioHang sanpham = lstGioHang.SingleOrDefault(n => n.iMaSP == iMaSP);

            if (sanpham != null)
            {
                lstGioHang.RemoveAll(n => n.iMaSP == iMaSP);
            }

            string cookieName = "GioHangCookie_" + tk.MaTK;
            if (lstGioHang.Count == 0)
            {
                HttpCookie gioHangCookie = new HttpCookie(cookieName);
                gioHangCookie.Expires = DateTime.Now.AddDays(-1);
                Response.Cookies.Add(gioHangCookie);

                return RedirectToAction("Index", "Home");
            }

            LuuGioHangVaoCookie(lstGioHang);
            return RedirectToAction("Index", "GioHang");
        }

        [HttpGet]
        public ActionResult DatHang()
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();
            if (lstGioHang == null || lstGioHang.Count == 0)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.TongSoLuong = lstGioHang.Sum(n => n.iSoLuong);
            decimal tongTienGoc = lstGioHang.Sum(n => n.dThanhTien);

            if (Session["TienGiam"] != null)
            {
                ViewBag.TienGiam = (decimal)Session["TienGiam"];
                ViewBag.TongTien = tongTienGoc - ViewBag.TienGiam;
            }
            else
            {
                ViewBag.TienGiam = 0m;
                ViewBag.TongTien = tongTienGoc;
            }

            return View(lstGioHang);
        }

        [HttpPost]
        public ActionResult DatHang(FormCollection collection)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            List<GioHang> lstGioHang = LayGioHang();

            try
            {
                DonHang dh = new DonHang();
                dh.NgayDat = DateTime.Now;
                dh.MaTK = tk.MaTK;

                dh.PhuongThucTT = collection["PhuongThucTT"];
                dh.HoTenNhan = collection["TenKhachHang"];
                dh.SDTNhan = collection["SoDienThoai"];
                dh.DiaChiNhan = collection["DiaChi"];

                decimal tongTienThucTe = lstGioHang.Sum(n => n.dThanhTien);
                if (Session["TienGiam"] != null)
                {
                    tongTienThucTe -= (decimal)Session["TienGiam"];
                }
                dh.TongTien = tongTienThucTe;

                if (dh.TongTien > 20000000 && dh.PhuongThucTT == "COD")
                {
                    TempData["LoiThanhToan"] = "Đơn hàng có giá trị trên 20 triệu VNĐ không được phép thanh toán khi nhận hàng (COD). Vui lòng chọn VNPAY hoặc MoMo.";
                    return RedirectToAction("DatHang", "GioHang");
                }

                db.DonHang.Add(dh);
                db.SaveChanges();

                foreach (var item in lstGioHang)
                {
                    ChiTietDonHang ctdh = new ChiTietDonHang();
                    ctdh.MaDH = dh.MaDH;
                    ctdh.MaSP = item.iMaSP;
                    ctdh.SoLuong = item.iSoLuong;
                    ctdh.GiaMua = item.dDonGia;
                    db.ChiTietDonHang.Add(ctdh);

                    SanPham sp = db.SanPham.Single(n => n.MaSP == item.iMaSP);
                    sp.SoLuongTon -= item.iSoLuong;
                }
                db.SaveChanges();

                // ========================================================
                // BIG UPDATE GIAI ĐOẠN 4: ĐỐT MÃ VOUCHER KHI ĐẶT HÀNG
                // ========================================================
                if (Session["Voucher"] != null)
                {
                    var sessionVoucher = Session["Voucher"] as MaGiamGia;

                    if (sessionVoucher.HienThiDoiThuong == true)
                    {
                        // 1. Nếu là mã điểm thưởng: Đổi trạng thái trong Ví thành Đã Sử Dụng (true)
                        var vi = db.ViVoucher.FirstOrDefault(v => v.MaTK == tk.MaTK && v.MaVoucher == sessionVoucher.MaVoucher && v.TrangThaiSD == false);
                        if (vi != null)
                        {
                            vi.TrangThaiSD = true;
                        }
                    }
                    else
                    {
                        // 2. Nếu là mã Public đại trà: Trừ số lượng trong kho MaGiamGia
                        var vcDb = db.MaGiamGia.SingleOrDefault(v => v.MaVoucher == sessionVoucher.MaVoucher);
                        if (vcDb != null && vcDb.SoLuong > 0)
                        {
                            vcDb.SoLuong--;
                        }
                    }
                    db.SaveChanges();
                }
                // ========================================================

                string cookieName = "GioHangCookie_" + tk.MaTK;

                if (dh.PhuongThucTT == "VNPAY")
                {
                    string vnp_Returnurl = Url.Action("VnPayReturn", "GioHang", null, Request.Url.Scheme);
                    string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
                    string vnp_TmnCode = "TL4PNZ3P";
                    string vnp_HashSecret = "FXWR3UJXVLAPWGGKQTLSMYGNFX705H3I";

                    VnPayLibrary vnpay = new VnPayLibrary();
                    vnpay.AddRequestData("vnp_Version", "2.1.0");
                    vnpay.AddRequestData("vnp_Command", "pay");
                    vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
                    vnpay.AddRequestData("vnp_Amount", ((long)(dh.TongTien * 100)).ToString());
                    vnpay.AddRequestData("vnp_BankCode", "");
                    vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                    vnpay.AddRequestData("vnp_CurrCode", "VND");
                    vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1");
                    vnpay.AddRequestData("vnp_Locale", "vn");
                    vnpay.AddRequestData("vnp_OrderInfo", "Thanh_toan_don_hang_" + dh.MaDH);
                    vnpay.AddRequestData("vnp_OrderType", "other");
                    vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
                    vnpay.AddRequestData("vnp_TxnRef", dh.MaDH.ToString());

                    string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                    return Redirect(paymentUrl);
                }
                else
                {
                    try
                    {
                        string senderEmail = "125khaaa@gmail.com";
                        string senderPassword = "gitmsjkogxoogola";
                        string receiverEmail = tk.Email;

                        string emailBody = $"<h2 style='color:#D70018;'>CẢM ƠN BẠN ĐÃ ĐẶT HÀNG TẠI CELLPHONES</h2>";
                        emailBody += $"<p>Xin chào <b>{dh.HoTenNhan}</b>,</p>";
                        emailBody += $"<p>Đơn hàng <b>#{dh.MaDH}</b> của bạn đã được tiếp nhận và đang chờ duyệt.</p>";

                        emailBody += "<table border='1' cellpadding='10' style='border-collapse: collapse; width: 100%; text-align: left;'>";
                        emailBody += "<tr style='background-color: #f8f9fa;'><th>Tên sản phẩm</th><th>Số lượng</th><th>Đơn giá</th></tr>";

                        foreach (var item in lstGioHang)
                        {
                            emailBody += $"<tr><td>{item.sTenSP}</td><td style='text-align:center;'>{item.iSoLuong}</td><td>{String.Format("{0:0,0}", item.dDonGia)}đ</td></tr>";
                        }
                        emailBody += "</table>";

                        emailBody += $"<h3 style='color:#D70018;'>Tổng thanh toán: {String.Format("{0:0,0}", dh.TongTien)} VNĐ</h3>";
                        emailBody += $"<p><b>Địa chỉ nhận hàng:</b> {dh.DiaChiNhan} | <b>SĐT:</b> {dh.SDTNhan}</p>";
                        emailBody += "<p>Chúng tôi sẽ sớm liên hệ để giao hàng. Chúc bạn một ngày vui vẻ!</p>";

                        System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage(
                            new System.Net.Mail.MailAddress(senderEmail, "CellphoneS System"),
                            new System.Net.Mail.MailAddress(receiverEmail)
                        );
                        mail.Subject = $"[CellphoneS] Xác nhận đơn hàng #{dh.MaDH}";
                        mail.Body = emailBody;
                        mail.IsBodyHtml = true;

                        System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
                        smtp.EnableSsl = true;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword);
                        smtp.Send(mail);
                    }
                    catch (System.Exception ex)
                    {
                        return Content("<h2 style='color:red;'>LỖI TỪ GOOGLE SMTP: </h2><p>" + ex.Message + "</p>");
                    }

                    HttpCookie gioHangCookie = new HttpCookie(cookieName);
                    gioHangCookie.Expires = DateTime.Now.AddDays(-1);
                    Response.Cookies.Add(gioHangCookie);

                    Session["Voucher"] = null;
                    Session["TienGiam"] = null;

                    return RedirectToAction("XacNhanDonHang", "GioHang");
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null)
                {
                    msg += "<br><b>Chi tiết sâu:</b> " + ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                    {
                        msg += "<br><b>Chi tiết gốc:</b> " + ex.InnerException.InnerException.Message;
                    }
                }
                return Content("<h2 style='color:red;'>LỖI CƠ SỞ DỮ LIỆU:</h2><p>" + msg + "</p><br/><p><i>Bạn hãy chụp màn hình dòng này gửi lại mình nhé!</i></p>");
            }
        }

        public ActionResult VnPayReturn()
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            if (Request.QueryString.AllKeys.Length > 0)
            {
                string vnp_HashSecret = "FXWR3UJXVLAPWGGKQTLSMYGNFX705H3I";
                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (string s in Request.QueryString)
                {
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(s, Request.QueryString[s]);
                    }
                }

                int maDH = int.Parse(vnpay.GetResponseData("vnp_TxnRef"));
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];

                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00")
                    {
                        DonHang dh = db.DonHang.SingleOrDefault(n => n.MaDH == maDH);
                        if (dh != null)
                        {
                            dh.TrangThaiTT = true;
                            dh.TrangThaiDH = "Chờ xử lý";
                            db.SaveChanges();

                            try
                            {
                                string senderEmail = "125khaaa@gmail.com";
                                string senderPassword = "gitmsjkogxoogola";
                                TaiKhoan khachHang = db.TaiKhoan.SingleOrDefault(t => t.MaTK == dh.MaTK);
                                string receiverEmail = khachHang != null ? khachHang.Email : "";

                                string emailBody = $"<h2 style='color:#28a745;'>XÁC NHẬN THANH TOÁN VNPAY THÀNH CÔNG</h2>";
                                emailBody += $"<p>Xin chào <b>{dh.HoTenNhan}</b>,</p>";
                                emailBody += $"<p>Hệ thống đã nhận được số tiền thanh toán trực tuyến cho đơn hàng <b>#{dh.MaDH}</b> qua cổng VNPAY.</p>";
                                emailBody += $"<h3 style='color:#D70018;'>Tổng thanh toán trực tuyến: {String.Format("{0:0,0}", dh.TongTien)} VNĐ</h3>";

                                System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage(
                                    new System.Net.Mail.MailAddress(senderEmail, "CellphoneS System"),
                                    new System.Net.Mail.MailAddress(receiverEmail)
                                );
                                mail.Subject = $"[CellphoneS] Đơn hàng #{dh.MaDH} thanh toán thành công";
                                mail.Body = emailBody;
                                mail.IsBodyHtml = true;

                                System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
                                smtp.EnableSsl = true;
                                smtp.UseDefaultCredentials = false;
                                smtp.Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword);
                                smtp.Send(mail);
                            }
                            catch { }
                        }

                        string cookieName = "GioHangCookie_" + tk.MaTK;
                        HttpCookie gioHangCookie = new HttpCookie(cookieName);
                        gioHangCookie.Expires = DateTime.Now.AddDays(-1);
                        Response.Cookies.Add(gioHangCookie);

                        Session["Voucher"] = null;
                        Session["TienGiam"] = null;

                        return RedirectToAction("XacNhanDonHang", "GioHang");
                    }
                    else
                    {
                        ViewBag.Message = "Giao dịch bị khách hàng hủy bỏ hoặc lỗi thẻ. Mã lỗi: " + vnp_ResponseCode;
                        return View("LoiThanhToan");
                    }
                }
            }
            ViewBag.Message = "Chữ ký bảo mật VNPAY không hợp lệ!";
            return View("LoiThanhToan");
        }

        public ActionResult XacNhanDonHang()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ApDungVoucher(string maCode)
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;

            List<GioHang> lstGioHang = LayGioHang();
            if (lstGioHang == null || lstGioHang.Count == 0)
            {
                TempData["LoiVoucher"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index");
            }

            maCode = maCode.Trim().ToUpper();
            var voucher = db.MaGiamGia.SingleOrDefault(v => v.MaVoucher == maCode);

            if (voucher == null)
            {
                TempData["LoiVoucher"] = "Mã giảm giá không tồn tại!";
                return RedirectToAction("Index");
            }

            if (DateTime.Now < voucher.NgayBatDau || DateTime.Now > voucher.NgayKetThuc)
            {
                TempData["LoiVoucher"] = "Mã giảm giá đã hết hạn hoặc chưa đến thời gian áp dụng!";
                return RedirectToAction("Index");
            }

            // ========================================================
            // BIG UPDATE GIAI ĐOẠN 4: KIỂM TRA MÃ CÓ TRONG VÍ KHÔNG
            // ========================================================
            if (voucher.HienThiDoiThuong == true)
            {
                if (tk == null)
                {
                    TempData["LoiVoucher"] = "Vui lòng đăng nhập để sử dụng mã ưu đãi từ Ví Voucher!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra khách hàng có mã này trong Ví và chưa sử dụng không
                var vi = db.ViVoucher.FirstOrDefault(v => v.MaTK == tk.MaTK && v.MaVoucher == maCode && v.TrangThaiSD == false);
                if (vi == null)
                {
                    TempData["LoiVoucher"] = "Bạn chưa đổi mã này hoặc mã đã được sử dụng!";
                    return RedirectToAction("Index");
                }
                // Lưu ý: Không check SoLuong <= 0 ở đây vì số lượng đã bị trừ lúc khách bấm đổi điểm lấy mã rồi.
            }
            else
            {
                // Nếu là mã đại trà (public) thì check xem kho mã còn không
                if (voucher.SoLuong <= 0)
                {
                    TempData["LoiVoucher"] = "Mã giảm giá này đã được sử dụng hết!";
                    return RedirectToAction("Index");
                }
            }
            // ========================================================

            decimal tongTienHienTai = (decimal)lstGioHang.Sum(n => n.dThanhTien);

            if (voucher.DonToiThieu.HasValue && tongTienHienTai < voucher.DonToiThieu.Value)
            {
                TempData["LoiVoucher"] = "Đơn hàng chưa đạt giá trị tối thiểu " + String.Format("{0:0,0}", voucher.DonToiThieu.Value).Replace(",", ".") + "đ để áp mã này!";
                return RedirectToAction("Index");
            }

            decimal tienGiam = (tongTienHienTai * voucher.PhanTramGiam) / 100;

            if (voucher.GiaTriToiDa.HasValue && tienGiam > voucher.GiaTriToiDa.Value)
            {
                tienGiam = voucher.GiaTriToiDa.Value;
            }

            Session["Voucher"] = voucher;
            Session["TienGiam"] = tienGiam;

            TempData["ThongBaoVoucher"] = "Áp dụng mã thành công! Bạn được giảm " + String.Format("{0:0,0}", tienGiam).Replace(",", ".") + "đ.";
            return RedirectToAction("Index");
        }

        public ActionResult XoaVoucher()
        {
            Session["Voucher"] = null;
            Session["TienGiam"] = null;
            return RedirectToAction("Index");
        }
    }
}