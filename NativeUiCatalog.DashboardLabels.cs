namespace Naufal_Windows_Tech_s_Powertoys;

internal static partial class NativeUiCatalog
{
    private const string DashboardLabels = """
en|Languages|Block|Run cleanup|Start|Stop|POWER PLAN|Apply|Check|Detect
id|Bahasa|Blokir|Jalankan pembersihan|Mulai|Hentikan|RENCANA DAYA|Terapkan|Periksa|Deteksi
de|Sprachen|Blockieren|Bereinigung ausführen|Starten|Stoppen|ENERGIESPARPLAN|Anwenden|Prüfen|Erkennen
fr|Langues|Bloquer|Lancer le nettoyage|Démarrer|Arrêter|MODE D’ALIMENTATION|Appliquer|Vérifier|Détecter
ar|اللغات|حظر|تشغيل التنظيف|بدء|إيقاف|خطة الطاقة|تطبيق|فحص|اكتشاف
tl|Mga wika|Harangan|Patakbuhin ang paglilinis|Simulan|Ihinto|PLANO NG KURYENTE|Ilapat|Suriin|Tukuyin
vi|Ngôn ngữ|Chặn|Chạy dọn dẹp|Bắt đầu|Dừng|CHẾ ĐỘ NGUỒN ĐIỆN|Áp dụng|Kiểm tra|Phát hiện
zh-CN|语言|阻止|运行清理|开始|停止|电源计划|应用|检查|检测
zh-TW|語言|封鎖|執行清理|開始|停止|電源計劃|套用|檢查|偵測
th|ภาษา|บล็อก|เรียกใช้การล้างข้อมูล|เริ่ม|หยุด|แผนการใช้พลังงาน|นำไปใช้|ตรวจสอบ|ตรวจหา
ru|Языки|Блокировать|Запустить очистку|Начать|Остановить|СХЕМА ЭЛЕКТРОПИТАНИЯ|Применить|Проверить|Обнаружить
uk|Мови|Блокувати|Запустити очищення|Почати|Зупинити|ПЛАН ЖИВЛЕННЯ|Застосувати|Перевірити|Виявити
pt|Idiomas|Bloquear|Executar limpeza|Iniciar|Parar|PLANO DE ENERGIA|Aplicar|Verificar|Detetar
ja|言語|ブロック|クリーンアップを実行|開始|停止|電源プラン|適用|確認|検出
ko|언어|차단|정리 실행|시작|중지|전원 관리 옵션|적용|확인|감지
ur|زبانیں|روکیں|صفائی چلائیں|شروع کریں|بند کریں|بجلی کا منصوبہ|لاگو کریں|جانچیں|شناخت کریں
ta|மொழிகள்|தடு|சுத்தப்படுத்தலை இயக்கு|தொடங்கு|நிறுத்து|மின் திட்டம்|பயன்படுத்து|சரிபார்|கண்டறி
hi|भाषाएँ|अवरोधित करें|सफ़ाई चलाएँ|शुरू करें|रोकें|पावर योजना|लागू करें|जाँचें|पता लगाएँ
ms|Bahasa|Sekat|Jalankan pembersihan|Mulakan|Hentikan|PELAN KUASA|Gunakan|Semak|Kesan
jv|Basa|Blokir|Jalanake reresik|Wiwiti|Mandhegake|RENCANA DAYA|Terapake|Priksa|Deteksi
ban|Basa|Blokir|Jalanang pangresikan|Wiwitin|Mandegang|RENCANA DAYA|Terapang|Priksa|Deteksi
sv|Språk|Blockera|Kör rensning|Starta|Stoppa|ENERGISCHEMA|Tillämpa|Kontrollera|Identifiera
es|Idiomas|Bloquear|Ejecutar limpieza|Iniciar|Detener|PLAN DE ENERGÍA|Aplicar|Comprobar|Detectar
""";

    // Targeted corrections to older translations that copied entire English
    // interface labels. Other languages keep their existing reviewed values.
    // Keys must already exist, so this cannot create unequal language coverage.
    private static readonly (string Language, string Key, string Value)[] ReviewedLabelOverrides =
    [
        ("tl", "SYSTEM & TUNING TOOLS", "MGA KAGAMITAN SA SISTEMA AT PAGSASAAYOS"),
        ("tl", "SYSTEM", "SISTEMA"),
        ("tl", "ADVANCED", "MAS MASUSING MGA OPSIYON"),
        ("tl", "Windows Activation", "Pag-activate ng Windows"),
        ("tl", "Office Activation", "Pag-activate ng Office"),
        ("tl", "BitLocker Manager", "Pamamahala ng BitLocker"),
        ("tl", "Gaming Tweaks", "Mga Pagsasaayos sa Paglalaro"),
        ("tl", "Games Runtime & Compatibility Check", "Pagsuri ng Runtime at Pagiging Tugma ng mga Laro"),
        ("tl", "GPU Driver Manager", "Pamamahala ng Driver ng GPU"),
        ("tl", "MSI Mode Utility", "Kagamitan sa MSI Mode"),
        ("tl", "PERFORMANCE PROFILE", "PROFILE NG PAGGANAP"),
        ("tl", "Competitive Gaming", "Kompetitibong Paglalaro"),
        ("tl", "Optimized Gaming", "Pinahusay na Paglalaro"),
        ("tl", "Balanced", "Balanse"),
        ("tl", "LIVE GAMING STATUS", "KASALUKUYANG KATAYUAN NG PAGLALARO"),
        ("tl", "CPU LIVE", "KASALUKUYANG CPU"),
        ("tl", "RAM LIVE", "KASALUKUYANG RAM"),
        ("tl", "GPU 3D LIVE", "KASALUKUYANG GPU 3D"),
        ("ta", "Games Runtime & Compatibility Check", "விளையாட்டு இயக்கச் சூழல் மற்றும் இணக்கத்தன்மைச் சரிபார்ப்பு"),
        ("hi", "Games Runtime & Compatibility Check", "गेम रनटाइम और संगतता जाँच"),
        ("ta", "MSI Mode Utility", "MSI பயன்முறைக் கருவி")
    ];
}
