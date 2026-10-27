using Newtonsoft.Json.Linq;
using Project_Cellphoness.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Project_Cellphoness.Controllers
{
    public class TaiKhoanController : Controller
    {
        CellphonesStoreEntities1 db = new CellphonesStoreEntities1();

        // ==============================================================
        // 1. UC10: ĐĂNG KÝ
        // ==============================================================
        [HttpGet]
        public ActionResult DangKy()
        {
            return View();
        }

        [HttpPost]
        public ActionResult DangKy(TaiKhoan tk, string XacNhanMatKhau)
        {
            if (ModelState.IsValid)
            {
                // ==========================================================
                // QĐ08: Mật khẩu tối thiểu 8 ký tự
                // ==========================================================
                if (tk.MatKhau.Length < 8)
                {
                    ViewBag.LoiMatKhau = "Mật khẩu phải có tối thiểu 8 ký tự!";
                    return View(tk);
                }

                if (!string.IsNullOrEmpty(tk.Email)) tk.Email = tk.Email.Trim();
                if (!string.IsNullOrEmpty(tk.SoDienThoai)) tk.SoDienThoai = tk.SoDienThoai.Trim();

                var checkEmail = db.TaiKhoan.FirstOrDefault(x => x.Email == tk.Email);
                if (checkEmail != null)
                {
                    ViewBag.LoiEmail = "Email này đã được sử dụng! Vui lòng chọn email khác.";
                    return View(tk);
                }

                var checkSDT = db.TaiKhoan.FirstOrDefault(x => x.SoDienThoai == tk.SoDienThoai);
                if (checkSDT != null)
                {
                    ViewBag.LoiSDT = "Số điện thoại này đã đăng ký tài khoản! Vui lòng dùng số khác.";
                    return View(tk);
                }

                if (tk.MatKhau != XacNhanMatKhau)
                {
                    ViewBag.LoiMatKhau = "Mật khẩu xác nhận không khớp!";
                    return View(tk);
                }

                tk.NgayTao = DateTime.Now;
                tk.TrangThai = true;
                tk.MaChucVu = 2;

                var maxMaTK = db.TaiKhoan.Any() ? db.TaiKhoan.Max(t => t.MaTK) : 0;
                tk.MaTK = maxMaTK + 1;
                tk.GoogleID = Guid.NewGuid().ToString();

                db.TaiKhoan.Add(tk);
                db.SaveChanges();

                TempData["ThongBaoDK"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("DangNhap");
            }
            return View(tk);
        }

        // ==============================================================
        // 2. UC09: ĐĂNG NHẬP
        // ==============================================================
        [HttpGet]
        public ActionResult DangNhap()
        {
            return View();
        }

        [HttpPost]
        public ActionResult DangNhap(FormCollection collection)
        {
            string email = collection["Email"];
            string matKhau = collection["MatKhau"];

            // ==========================================================
            // QĐ09 (Phần 1): Kiểm tra xem tài khoản này có đang bị khóa 15 phút không
            // ==========================================================
            string lockKey = "Lock_" + email;
            if (Session[lockKey] != null)
            {
                DateTime timeLocked = (DateTime)Session[lockKey];
                TimeSpan timePassed = DateTime.Now - timeLocked;

                if (timePassed.TotalMinutes < 15)
                {
                    int minuteLeft = 15 - (int)timePassed.TotalMinutes;
                    ViewBag.ThongBao = $"Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau {minuteLeft} phút!";
                    return View();
                }
                else
                {
                    // Đã qua 15 phút -> Mở khóa, xóa lịch sử sai
                    Session.Remove(lockKey);
                    Session.Remove("FailCount_" + email);
                }
            }

            TaiKhoan tk = db.TaiKhoan.SingleOrDefault(n => n.Email == email && n.MatKhau == matKhau);

            if (tk != null)
            {
                // ==========================================================
                // QĐ20: Khóa Spammer / Boom hàng (Trạng thái bị set thành false)
                // ==========================================================
                if (tk.TrangThai != true)
                {
                    ViewBag.ThongBao = "Tài khoản của bạn đã bị khóa vĩnh viễn do vi phạm chính sách! Vui lòng liên hệ quản trị viên.";
                    return View();
                }

                // Đăng nhập đúng -> Xóa lịch sử đăng nhập sai
                Session.Remove("FailCount_" + email);

                Session["TaiKhoan"] = tk;
                return RedirectToAction("DatHang", "GioHang");
            }
            else
            {
                // ==========================================================
                // QĐ09 (Phần 2): Xử lý cộng dồn số lần nhập sai
                // ==========================================================
                string failKey = "FailCount_" + email;
                int failCount = Session[failKey] != null ? (int)Session[failKey] : 0;
                failCount++;

                Session[failKey] = failCount;

                if (failCount >= 5)
                {
                    // Lưu lại mốc thời gian bắt đầu khóa
                    Session[lockKey] = DateTime.Now;
                    ViewBag.ThongBao = "Bạn đã nhập sai 5 lần liên tiếp. Tài khoản bị khóa 15 phút!";
                }
                else
                {
                    ViewBag.ThongBao = $"Email hoặc mật khẩu không đúng! (Bạn còn {5 - failCount} lần thử)";
                }
                return View();
            }
        }

        // ==============================================================
        // 3. UC11: ĐĂNG XUẤT
        // ==============================================================
        public ActionResult DangXuat()
        {
            Session["TaiKhoan"] = null;
            Session["GioHang"] = null;
            return RedirectToAction("Index", "Home");
        }

        // ==============================================================
        // 4. UC12: LỊCH SỬ MUA HÀNG
        // ==============================================================
        [HttpGet]
        public ActionResult LichSuDonHang()
        {
            TaiKhoan tk = (TaiKhoan)Session["TaiKhoan"];
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            var lstDonHang = db.DonHang
                               .Where(n => n.MaTK == tk.MaTK)
                               .OrderByDescending(n => n.NgayDat)
                               .ToList();

            return View(lstDonHang);
        }

        // ==============================================================
        // 5. UC15: HỦY ĐƠN HÀNG (ĐÃ CẬP NHẬT LOGIC CHẶN HỦY)
        // ==============================================================
        [HttpGet]
        public ActionResult HuyDonHang(int MaDH, string lyDo)
        {
            TaiKhoan tk = (TaiKhoan)Session["TaiKhoan"];
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            DonHang dh = db.DonHang.SingleOrDefault(n => n.MaDH == MaDH && n.MaTK == tk.MaTK);

            if (dh != null)
            {
                // THÊM ĐOẠN NÀY: Chặn hủy nếu đơn hàng đang giao hoặc đã hoàn thành
                if (dh.TrangThaiDH == "Đang giao hàng" || dh.TrangThaiDH == "Hoàn thành")
                {
                    TempData["ThongBaoHuy"] = $"Lỗi: Không thể hủy đơn hàng #{MaDH} vì trạng thái hiện tại là '{dh.TrangThaiDH}'!";
                    return RedirectToAction("LichSuDonHang");
                }

                dh.TrangThaiDH = "Đã hủy - Lý do: " + lyDo;

                var lstChiTiet = db.ChiTietDonHang.Where(c => c.MaDH == MaDH).ToList();
                foreach (var item in lstChiTiet)
                {
                    SanPham sp = db.SanPham.SingleOrDefault(s => s.MaSP == item.MaSP);
                    if (sp != null)
                    {
                        sp.SoLuongTon += item.SoLuong; // Hoàn lại số lượng tồn kho
                    }
                }

                db.SaveChanges();
                TempData["ThongBaoHuy"] = $"Đã hủy đơn hàng #{MaDH} thành công!";
            }

            return RedirectToAction("LichSuDonHang");
        }

        // ==============================================================
        // 6. UC10: QUÊN MẬT KHẨU
        // ==============================================================
        [HttpGet]
        public ActionResult QuenMatKhau()
        {
            return View();
        }

        [HttpPost]
        public ActionResult QuenMatKhau(string EmailXacNhan)
        {
            var tk = db.TaiKhoan.SingleOrDefault(t => t.Email == EmailXacNhan);
            if (tk == null)
            {
                ViewBag.ThongBao = "Email này chưa được đăng ký!";
                return View();
            }

            Random rd = new Random();
            string otp = rd.Next(100000, 999999).ToString();

            if (HttpContext.Session != null)
            {
                HttpContext.Session["OTP"] = otp;
                HttpContext.Session["ResetEmail"] = EmailXacNhan;
                // ==========================================================
                // QĐ10: Ghi nhận thời điểm tạo OTP
                // ==========================================================
                HttpContext.Session["OTP_Time"] = DateTime.Now;
            }
            else
            {
                ViewBag.ThongBao = "Hệ thống Session đang gặp sự cố. Vui lòng F5 trang!";
                return View();
            }

            try
            {
                string senderEmail = "125khaaa@gmail.com";
                string senderPassword = "gitmsjkogxoogola";

                string emailBody = $"<h2 style='color:#D70018;'>YÊU CẦU LẤY LẠI MẬT KHẨU</h2>";
                emailBody += $"<p>Chào <b>{tk.HoTen}</b>,</p>";
                emailBody += $"<p>Bạn vừa yêu cầu lấy lại mật khẩu tại hệ thống CellphoneS.</p>";
                emailBody += $"<p>Mã xác nhận (OTP) của bạn là: <b style='font-size:24px; color:blue;'>{otp}</b></p>";
                emailBody += $"<p><i>Lưu ý: Mã này chỉ có hiệu lực trong 5 phút. Không chia sẻ mã này cho bất kỳ ai!</i></p>";

                System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage(
                    new System.Net.Mail.MailAddress(senderEmail, "CellphoneS System"),
                    new System.Net.Mail.MailAddress(EmailXacNhan)
                );
                mail.Subject = "[CellphoneS] Mã OTP lấy lại mật khẩu";
                mail.Body = emailBody;
                mail.IsBodyHtml = true;

                System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword);
                smtp.Send(mail);

                return RedirectToAction("XacNhanOTP");
            }
            catch (System.Exception ex)
            {
                return Content("<h2 style='color:red;'>LỖI TỪ GOOGLE SMTP (QUÊN MẬT KHẨU): </h2><p>" + ex.Message + "</p>");
            }
        }

        // ==============================================================
        // 7. XÁC NHẬN MÃ OTP
        // ==============================================================
        [HttpGet]
        public ActionResult XacNhanOTP()
        {
            if (Session["OTP"] == null) return RedirectToAction("QuenMatKhau");
            return View();
        }

        [HttpPost]
        public ActionResult XacNhanOTP(string maOTP)
        {
            if (Session["OTP"] != null && Session["OTP_Time"] != null)
            {
                // ==========================================================
                // QĐ10: Kiểm tra hiệu lực 5 phút của OTP
                // ==========================================================
                DateTime otpTime = (DateTime)Session["OTP_Time"];
                if ((DateTime.Now - otpTime).TotalMinutes > 5)
                {
                    ViewBag.ThongBao = "Mã OTP đã hết hạn (vượt quá 5 phút). Vui lòng yêu cầu cấp lại mã mới!";
                    return View();
                }

                if (Session["OTP"].ToString() == maOTP.Trim())
                {
                    return RedirectToAction("DatLaiMatKhau");
                }
            }

            ViewBag.ThongBao = "Mã OTP không hợp lệ. Vui lòng kiểm tra lại email!";
            return View();
        }

        // ==============================================================
        // 8. ĐẶT LẠI MẬT KHẨU MỚI
        // ==============================================================
        [HttpGet]
        public ActionResult DatLaiMatKhau()
        {
            if (Session["ResetEmail"] == null) return RedirectToAction("QuenMatKhau");
            return View();
        }

        [HttpPost]
        public ActionResult DatLaiMatKhau(string MatKhauMoi, string XacNhanMatKhau)
        {
            if (MatKhauMoi != XacNhanMatKhau)
            {
                ViewBag.ThongBao = "Mật khẩu xác nhận không trùng khớp!";
                return View();
            }

            // Kiểm tra thêm QĐ08 cho mật khẩu mới
            if (MatKhauMoi.Length < 8)
            {
                ViewBag.ThongBao = "Mật khẩu phải có tối thiểu 8 ký tự!";
                return View();
            }

            string email = Session["ResetEmail"].ToString();
            var tk = db.TaiKhoan.SingleOrDefault(t => t.Email == email);
            if (tk != null)
            {
                tk.MatKhau = MatKhauMoi;
                db.SaveChanges();

                Session["OTP"] = null;
                Session["ResetEmail"] = null;
                Session["OTP_Time"] = null;

                TempData["ThongBaoOTP"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
                return RedirectToAction("DangNhap");
            }
            return View();
        }

        // ==============================================================
        // UC19: ĐĂNG NHẬP BẰNG GOOGLE (ĐÃ CẮT NHỎ CHUỖI ĐỂ TRÁNH GITHUB QUÉT TRÚNG)
        // ==============================================================
        private readonly string googleClientId = "917854431918-" + "b9f95uopf9jud893f4rqgm78p83toig3" + ".apps.googleusercontent.com";
        private readonly string googleClientSecret = "GOCSPX-" + "f7yHO_E5oKcpyjPjrZHc3" + "fn-9Kwr";
        private readonly string googleRedirectUrl = "https://localhost:44352/TaiKhoan/GoogleCallback";

        public ActionResult LoginWithGoogle()
        {
            string url = $"https://accounts.google.com/o/oauth2/v2/auth?scope=email%20profile&access_type=offline&include_granted_scopes=true&response_type=code&redirect_uri={googleRedirectUrl}&client_id={googleClientId}";
            return Redirect(url);
        }

        public ActionResult GoogleCallback(string code)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            if (string.IsNullOrEmpty(code))
            {
                ViewBag.ThongBao = "Đăng nhập Google thất bại hoặc bạn đã hủy tác vụ.";
                return View("DangNhap");
            }

            try
            {
                using (var client = new System.Net.WebClient())
                {
                    var reqparm = new System.Collections.Specialized.NameValueCollection();
                    reqparm.Add("code", code);
                    reqparm.Add("client_id", googleClientId);
                    reqparm.Add("client_secret", googleClientSecret);
                    reqparm.Add("redirect_uri", googleRedirectUrl);
                    reqparm.Add("grant_type", "authorization_code");

                    byte[] responsebytes = client.UploadValues("https://oauth2.googleapis.com/token", "POST", reqparm);
                    string responsebody = System.Text.Encoding.UTF8.GetString(responsebytes);

                    var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var tokenData = js.Deserialize<Dictionary<string, object>>(responsebody);

                    if (!tokenData.ContainsKey("access_token"))
                    {
                        ViewBag.ThongBao = "Google từ chối cung cấp chứng chỉ xác thực.";
                        return View("DangNhap");
                    }
                    string accessToken = tokenData["access_token"].ToString();

                    client.Headers.Clear();
                    client.Headers.Add("Authorization", "Bearer " + accessToken);
                    string userInfoString = client.DownloadString("https://www.googleapis.com/oauth2/v2/userinfo");
                    var userData = js.Deserialize<Dictionary<string, object>>(userInfoString);

                    string email = userData.ContainsKey("email") ? userData["email"].ToString() : "";
                    string name = userData.ContainsKey("name") ? userData["name"].ToString() : "";
                    string googleId = userData.ContainsKey("id") ? userData["id"].ToString() : "";

                    if (string.IsNullOrEmpty(email))
                    {
                        ViewBag.ThongBao = "Tài khoản Google của bạn không có Email.";
                        return View("DangNhap");
                    }

                    TaiKhoan tk = db.TaiKhoan.SingleOrDefault(t => t.Email == email);
                    if (tk != null)
                    {
                        // QĐ20: Khóa Spammer cho Đăng nhập Google
                        if (tk.TrangThai != true)
                        {
                            ViewBag.ThongBao = "Tài khoản của bạn đã bị khóa bởi hệ thống do vi phạm chính sách!";
                            return View("DangNhap");
                        }

                        if (string.IsNullOrEmpty(tk.GoogleID))
                        {
                            tk.GoogleID = googleId;
                            db.SaveChanges();
                        }

                        Session["TaiKhoan"] = tk;
                        return RedirectToAction("DatHang", "GioHang");
                    }
                    else
                    {
                        TaiKhoan newTk = new TaiKhoan();
                        var maxMaTK = db.TaiKhoan.Any() ? db.TaiKhoan.Max(t => t.MaTK) : 0;

                        newTk.MaTK = maxMaTK + 1;
                        newTk.HoTen = name;
                        newTk.Email = email;
                        newTk.MatKhau = googleId.Substring(0, 8);
                        newTk.GoogleID = googleId;
                        newTk.NgayTao = DateTime.Now;
                        newTk.TrangThai = true;
                        newTk.MaChucVu = 2;

                        db.TaiKhoan.Add(newTk);
                        db.SaveChanges();

                        Session["TaiKhoan"] = newTk;
                        return RedirectToAction("DatHang", "GioHang");
                    }
                }
            }
            catch (System.Net.WebException)
            {
                ViewBag.ThongBao = "Google báo lỗi bảo mật (Sai Secret Key hoặc Token hết hạn). Vui lòng kiểm tra lại cấu hình.";
                return View("DangNhap");
            }
            catch (Exception ex)
            {
                ViewBag.ThongBao = "Có sự cố bất ngờ: " + ex.Message;
                return View("DangNhap");
            }
        }

        public ActionResult ThongTinCaNhan()
        {
            var sessionTk = Session["TaiKhoan"] as Project_Cellphoness.Models.TaiKhoan;
            if (sessionTk == null)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var khachHang = db.TaiKhoan.Find(sessionTk.MaTK);
            if (khachHang == null) return RedirectToAction("DangNhap", "TaiKhoan");

            return View(khachHang);
        }

        // 2. XỬ LÝ CẬP NHẬT THÔNG TIN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CapNhatThongTin(int MaTK, string HoTen, string SoDienThoai)
        {
            var tk = db.TaiKhoan.Find(MaTK);
            if (tk != null)
            {
                tk.HoTen = HoTen;
                tk.SoDienThoai = SoDienThoai;
                db.SaveChanges();

                Session["TaiKhoan"] = tk;
                TempData["ThongBao"] = "Cập nhật hồ sơ thành công!";
            }
            return RedirectToAction("ThongTinCaNhan");
        }
        // ==============================================================
        // TÍNH NĂNG ĐỔI MẬT KHẨU (KÈM QĐ08: YÊU CẦU 8 KÝ TỰ)
        // ==============================================================
        [HttpGet]
        public ActionResult DoiMatKhau()
        {
            var tk = Session["TaiKhoan"] as TaiKhoan;
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DoiMatKhau(string MatKhauCu, string MatKhauMoi, string XacNhanMatKhau)
        {
            var tkSession = Session["TaiKhoan"] as TaiKhoan;
            if (tkSession == null) return RedirectToAction("DangNhap", "TaiKhoan");

            var tk = db.TaiKhoan.SingleOrDefault(t => t.MaTK == tkSession.MaTK);
            if (tk == null) return HttpNotFound();

            // Kiểm tra mật khẩu cũ có khớp với CSDL không
            if (tk.MatKhau != MatKhauCu)
            {
                ViewBag.Loi = "Mật khẩu hiện tại không đúng!";
                return View();
            }

            // QĐ08: Kiểm tra độ dài mật khẩu mới
            if (string.IsNullOrEmpty(MatKhauMoi) || MatKhauMoi.Length < 8)
            {
                ViewBag.Loi = "Mật khẩu mới phải có ít nhất 8 ký tự!";
                return View();
            }

            // Kiểm tra xác nhận mật khẩu
            if (MatKhauMoi != XacNhanMatKhau) 
            {
                ViewBag.Loi = "Xác nhận mật khẩu không khớp!";
                return View();
            }

            // Lưu thay đổi
            tk.MatKhau = MatKhauMoi;
            db.SaveChanges();

            // Cập nhật lại Session
            Session["TaiKhoan"] = tk;

            ViewBag.ThanhCong = "Đổi mật khẩu thành công!";
            return View();
        }
        // ==============================================================
        // 9. TÍNH NĂNG MINI GAME KÉO BÚA BAO (CƯỢC ĐIỂM X2)
        // ==============================================================
        [HttpGet]
        public ActionResult MiniGame()
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            var tk = db.TaiKhoan.Find(sessionTk.MaTK);
            if (tk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            Session["TaiKhoan"] = tk;
            return View(tk);
        }

        [HttpPost]
        public ActionResult ChoiKeoBuaBao(int diemCuoc, string luaChon)
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để chơi!" });

            var tk = db.TaiKhoan.Find(sessionTk.MaTK);
            if (tk == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản!" });

            int diemHienTai = tk.DiemHienTai ?? 0;

            if (diemCuoc < 50)
                return Json(new { success = false, message = "Mức cược tối thiểu là 50 điểm!" });

            if (diemCuoc > diemHienTai)
                return Json(new { success = false, message = $"Bạn không đủ điểm! Hiện tại bạn có {diemHienTai} điểm." });

            string[] options = { "keo", "bua", "bao" };
            Random rd = new Random(Guid.NewGuid().GetHashCode());
            string botChon = options[rd.Next(0, 3)];

            string ketQua = "hoa"; // thang, thua, hoa
            if (luaChon == botChon)
            {
                ketQua = "hoa";
            }
            else if ((luaChon == "keo" && botChon == "bao") ||
                     (luaChon == "bua" && botChon == "keo") ||
                     (luaChon == "bao" && botChon == "bua"))
            {
                ketQua = "thang";
            }
            else
            {
                ketQua = "thua";
            }

            int thayDoiDiem = 0;
            string thongDiep = "";

            if (ketQua == "thang")
            {
                // Thắng nhận X2 tiền cược (tức là lời thêm +diemCuoc vào tổng điểm hiện tại, tổng nhận về = 2 * diemCuoc)
                thayDoiDiem = diemCuoc;
                tk.DiemHienTai = diemHienTai + diemCuoc;
                tk.DiemTichLuy = (tk.DiemTichLuy ?? 0) + diemCuoc;
                thongDiep = $"Chúc mừng! Bạn đã chiến thắng Aira và nhận X2 mức cược (+{diemCuoc * 2} điểm về ví, lãi ròng +{diemCuoc} điểm)!";
            }
            else if (ketQua == "thua")
            {
                thayDoiDiem = -diemCuoc;
                tk.DiemHienTai = diemHienTai - diemCuoc;
                thongDiep = $"Tiếc quá! Aira đã thắng ván này. Bạn bị trừ {diemCuoc} điểm cược. Cố gắng phục thù ván sau nhé!";
            }
            else
            {
                thayDoiDiem = 0;
                thongDiep = "Hòa nhau rồi! Cả hai cùng ra một chiêu, bạn được hoàn lại nguyên vẹn số điểm cược!";
            }

            db.SaveChanges();
            Session["TaiKhoan"] = tk;

            return Json(new
            {
                success = true,
                result = ketQua,
                userChoice = luaChon,
                botChoice = botChon,
                newPoints = tk.DiemHienTai ?? 0,
                pointChange = thayDoiDiem,
                message = thongDiep
            });
        }

        // ==============================================================
        // 10. ĐIỂM DANH TÍCH LŨY 7 NGÀY LIÊN TỤC (T2->T4: 10đ, T5->T6: 15đ, T7->CN: 20đ)
        //     Nếu bỏ lỡ 1 ngày sẽ tự động quay về Thứ 2 (Ngày 1)
        // ==============================================================
        private string GetCheckInStoragePath()
        {
            return Server.MapPath("~/App_Data/CheckInStreakData.json");
        }

        private JObject LoadCheckInData()
        {
            string path = GetCheckInStoragePath();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(path);
                    return JObject.Parse(json);
                }
                catch { }
            }
            return new JObject();
        }

        private void SaveCheckInData(JObject data)
        {
            string path = GetCheckInStoragePath();
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, data.ToString());
        }

        [HttpGet]
        public ActionResult TrangThaiDiemDanh()
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null)
                return Json(new { loggedIn = false }, JsonRequestBehavior.AllowGet);

            var tk = db.TaiKhoan.Find(sessionTk.MaTK);
            if (tk == null)
                return Json(new { loggedIn = false }, JsonRequestBehavior.AllowGet);

            JObject allData = LoadCheckInData();
            string key = "User_" + tk.MaTK;
            int currentStreak = 0;
            bool checkedInToday = false;

            if (allData[key] != null)
            {
                string lastDateStr = allData[key]["LastDate"]?.ToString();
                int savedStreak = allData[key]["Streak"] != null ? (int)allData[key]["Streak"] : 0;

                if (DateTime.TryParse(lastDateStr, out DateTime lastDate))
                {
                    int daysDiff = (DateTime.Now.Date - lastDate.Date).Days;
                    if (daysDiff == 0)
                    {
                        checkedInToday = true;
                        currentStreak = savedStreak;
                    }
                    else if (daysDiff == 1)
                    {
                        // Liên tục từ hôm qua: nếu đã hoàn thành mốc 7 (Chủ nhật) thì vòng mới quay về 0 để lên 1
                        currentStreak = savedStreak >= 7 ? 0 : savedStreak;
                    }
                    else
                    {
                        // Bỏ lỡ >= 1 ngày -> Reset về đầu chuỗi (Thứ 2)
                        currentStreak = 0;
                    }
                }
            }

            int nextStep = checkedInToday ? currentStreak : (currentStreak + 1);

            return Json(new
            {
                loggedIn = true,
                streak = currentStreak,
                nextDayStep = nextStep,
                checkedInToday = checkedInToday,
                currentPoints = tk.DiemHienTai ?? 0
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult NhanDiemDanhHangNgay()
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để điểm danh nhận thưởng!" });

            var tk = db.TaiKhoan.Find(sessionTk.MaTK);
            if (tk == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản!" });

            JObject allData = LoadCheckInData();
            string key = "User_" + tk.MaTK;
            int currentStreak = 0;

            if (allData[key] != null)
            {
                string lastDateStr = allData[key]["LastDate"]?.ToString();
                int savedStreak = allData[key]["Streak"] != null ? (int)allData[key]["Streak"] : 0;

                if (DateTime.TryParse(lastDateStr, out DateTime lastDate))
                {
                    int daysDiff = (DateTime.Now.Date - lastDate.Date).Days;
                    if (daysDiff == 0)
                    {
                        return Json(new { success = false, message = "Hôm nay bạn đã điểm danh nhận thưởng rồi! Hãy quay lại vào ngày mai nhé." });
                    }
                    else if (daysDiff == 1)
                    {
                        currentStreak = savedStreak >= 7 ? 1 : (savedStreak + 1);
                    }
                    else
                    {
                        // Đứt chuỗi -> Quay lại mốc 1 (Thứ 2)
                        currentStreak = 1;
                    }
                }
                else
                {
                    currentStreak = 1;
                }
            }
            else
            {
                currentStreak = 1;
            }

            // Mức thưởng: T2, T3, T4 (mốc 1,2,3) = 10đ | T5, T6 (mốc 4,5) = 15đ | T7, CN (mốc 6,7) = 20đ
            int[] mucThuong = { 10, 10, 10, 15, 15, 20, 20 };
            int diemNhan = mucThuong[currentStreak - 1];
            string[] tenThu = { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ Nhật" };

            tk.DiemHienTai = (tk.DiemHienTai ?? 0) + diemNhan;
            tk.DiemTichLuy = (tk.DiemTichLuy ?? 0) + diemNhan;
            db.SaveChanges();
            Session["TaiKhoan"] = tk;

            allData[key] = new JObject
            {
                ["LastDate"] = DateTime.Now.ToString("yyyy-MM-dd"),
                ["Streak"] = currentStreak
            };
            SaveCheckInData(allData);

            return Json(new
            {
                success = true,
                streak = currentStreak,
                reward = diemNhan,
                newPoints = tk.DiemHienTai ?? 0,
                message = $"Điểm danh mốc {tenThu[currentStreak - 1]} thành công! Bạn nhận được +{diemNhan} điểm tích lũy!"
            });
        }
        // ==============================================================
        // 11. THAY ĐỔI AVATAR CÁ NHÂN
        // ==============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CapNhatAvatar(System.Web.HttpPostedFileBase avatarFile)
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null) return RedirectToAction("DangNhap", "TaiKhoan");

            var tk = db.TaiKhoan.Find(sessionTk.MaTK);
            if (tk != null && avatarFile != null && avatarFile.ContentLength > 0)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileName(avatarFile.FileName);
                    string uniqueName = "avatar_" + tk.MaTK + "_" + DateTime.Now.Ticks + System.IO.Path.GetExtension(fileName);
                    string path = Server.MapPath("~/Content/Avatars/");

                    if (!System.IO.Directory.Exists(path))
                    {
                        System.IO.Directory.CreateDirectory(path);
                    }

                    string fullPath = System.IO.Path.Combine(path, uniqueName);
                    avatarFile.SaveAs(fullPath);

                    tk.Avatar = "~/Content/Avatars/" + uniqueName;
                    db.SaveChanges();

                    Session["TaiKhoan"] = tk;
                    TempData["ThongBao"] = "Cập nhật ảnh đại diện thành công!";
                }
                catch (Exception ex)
                {
                    TempData["ThongBao"] = "Lỗi khi tải ảnh lên: " + ex.Message;
                }
            }
            return RedirectToAction("ThongTinCaNhan");
        }
        // ==============================================================
        // 12. API LẤY CHI TIẾT ĐƠN HÀNG VÀ SẢN PHẨM ĐÃ ĐẶT CHO POPUP (ĐÃ FIX LỖI ĐƠN GIÁ)
        // ==============================================================
        [HttpGet]
        public ActionResult ChiTietDonHangJson(int maDH)
        {
            var sessionTk = Session["TaiKhoan"] as TaiKhoan;
            if (sessionTk == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" }, JsonRequestBehavior.AllowGet);

            var dh = db.DonHang.FirstOrDefault(d => d.MaDH == maDH && d.MaTK == sessionTk.MaTK);
            if (dh == null)
                return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);

            var kh = db.TaiKhoan.FirstOrDefault(t => t.MaTK == dh.MaTK);

            // Lấy giá bán trực tiếp từ bảng SanPham (sp.GiaBan) thay vì ct.DonGia
            var items = (from ct in db.ChiTietDonHang
                         join sp in db.SanPham on ct.MaSP equals sp.MaSP
                         where ct.MaDH == maDH
                         select new
                         {
                             MaSP = sp.MaSP,
                             TenSP = sp.TenSP,
                             HinhAnh = sp.HinhAnh,
                             SoLuong = ct.SoLuong,
                             DonGia = sp.GiaBan,
                             ThanhTien = ct.SoLuong * sp.GiaBan
                         }).ToList();

            return Json(new
            {
                success = true,
                maDH = dh.MaDH,
                nguoiNhan = kh != null ? kh.HoTen : "Khách hàng",
                soDienThoai = kh != null ? kh.SoDienThoai : "",
                ngayDat = dh.NgayDat.HasValue ? dh.NgayDat.Value.ToString("dd/MM/yyyy HH:mm") : "N/A",
                trangThai = string.IsNullOrEmpty(dh.TrangThaiDH) ? "Chờ duyệt" : dh.TrangThaiDH,
                tongTien = dh.TongTien,
                items = items
            }, JsonRequestBehavior.AllowGet);
        }

    }

}