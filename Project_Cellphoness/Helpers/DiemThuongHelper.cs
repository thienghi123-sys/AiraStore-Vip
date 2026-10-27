using System;

namespace Project_Cellphoness.Helpers
{
    public class DiemThuongHelper
    {
        // Hàm tính hạng thành viên dựa vào điểm tích lũy
        public static string LayHangThanhVien(int? diemTichLuy)
        {
            // Nếu điểm null thì coi như là 0
            int diem = diemTichLuy ?? 0;

            if (diem >= 100000) return "Aira";
            if (diem >= 50000) return "Cao thủ";
            if (diem >= 20000) return "Tinh anh";
            if (diem >= 5000) return "Bạch kim";
            if (diem >= 1000) return "Vàng";
            if (diem >= 100) return "Bạc";

            return "Thành viên mới"; // Dưới 100 điểm
        }

        // Hàm tính số điểm sẽ được cộng khi mua hàng (100k = 10 điểm -> 10k = 1 điểm)
        public static int TinhDiemTuHoaDon(decimal tongTien)
        {
            return (int)(tongTien / 10000);
        }
    }
}