namespace Naufal_Windows_Tech_s_Powertoys;

internal static partial class NativeUiCatalog
{
    private const string Availability = """
en|Unavailable on this PC|{0} out of {1} have been verified, but {2} tweaks can't be applied due to unavailability on this PC.|{0}% processed — {1}/{2}|Verification failed for {0} item(s). See the progress window for details.|UNAVAILABLE
id|Tidak tersedia di PC ini|{0} dari {1} telah diverifikasi, tetapi {2} tweak tidak dapat diterapkan karena tidak tersedia di PC ini.|{0}% diproses — {1}/{2}|Verifikasi gagal untuk {0} opsi. Lihat detail di jendela progres.|TIDAK TERSEDIA
de|Auf diesem PC nicht verfügbar|{0} von {1} wurden überprüft, aber {2} Optimierungen können nicht angewendet werden, da sie auf diesem PC nicht verfügbar sind.|{0}% verarbeitet — {1}/{2}|Überprüfung für {0} Einträge fehlgeschlagen. Details stehen im Fortschrittsfenster.|NICHT VERFÜGBAR
fr|Indisponible sur ce PC|{0} sur {1} ont été vérifiés, mais {2} réglages ne peuvent pas être appliqués car ils sont indisponibles sur ce PC.|{0}% traités — {1}/{2}|Échec de la vérification de {0} éléments. Consultez les détails dans la fenêtre de progression.|INDISPONIBLE
ar|غير متاح على هذا الكمبيوتر|تم التحقق من {0} من أصل {1}، لكن لا يمكن تطبيق {2} من التعديلات لعدم توفرها على هذا الكمبيوتر.|تمت معالجة {0}% — {1}/{2}|فشل التحقق من {0} عناصر. راجع التفاصيل في نافذة التقدم.|غير متاح
tl|Hindi available sa PC na ito|Na-verify ang {0} sa {1}, ngunit hindi mailalapat ang {2} tweak dahil hindi available ang mga ito sa PC na ito.|{0}% naproseso — {1}/{2}|Nabigo ang pag-verify ng {0} item. Tingnan ang detalye sa window ng progreso.|HINDI AVAILABLE
vi|Không có trên PC này|Đã xác minh {0} trên {1}, nhưng không thể áp dụng {2} tinh chỉnh vì không có trên PC này.|Đã xử lý {0}% — {1}/{2}|Xác minh thất bại với {0} mục. Xem chi tiết trong cửa sổ tiến trình.|KHÔNG CÓ
zh-CN|此电脑上不可用|已验证 {1} 项中的 {0} 项，但有 {2} 项调整因在此电脑上不可用而无法应用。|已处理 {0}% — {1}/{2}|有 {0} 项验证失败。请在进度窗口中查看详情。|不可用
zh-TW|此電腦上無法使用|已驗證 {1} 項中的 {0} 項，但有 {2} 項調整因在此電腦上無法使用而無法套用。|已處理 {0}% — {1}/{2}|有 {0} 項驗證失敗。請在進度視窗中查看詳細資訊。|無法使用
th|ไม่พร้อมใช้งานบนพีซีนี้|ตรวจสอบแล้ว {0} จาก {1} รายการ แต่ไม่สามารถใช้การปรับแต่ง {2} รายการได้ เนื่องจากไม่พร้อมใช้งานบนพีซีนี้|ประมวลผลแล้ว {0}% — {1}/{2}|ตรวจสอบไม่สำเร็จ {0} รายการ ดูรายละเอียดในหน้าต่างความคืบหน้า|ไม่พร้อมใช้งาน
ru|Недоступно на этом ПК|Проверено {0} из {1}, но {2} настройки нельзя применить, поскольку они недоступны на этом ПК.|Обработано {0}% — {1}/{2}|Не удалось проверить {0} элементов. Подробности в окне выполнения.|НЕДОСТУПНО
uk|Недоступно на цьому ПК|Перевірено {0} із {1}, але {2} налаштування неможливо застосувати, оскільки вони недоступні на цьому ПК.|Оброблено {0}% — {1}/{2}|Не вдалося перевірити {0} елементів. Докладніше у вікні виконання.|НЕДОСТУПНО
pt|Indisponível neste PC|Foram verificados {0} de {1}, mas {2} ajustes não podem ser aplicados por estarem indisponíveis neste PC.|{0}% processados — {1}/{2}|Falha na verificação de {0} itens. Consulte os detalhes na janela de progresso.|INDISPONÍVEL
ja|この PC では利用できません|{1} 件中 {0} 件を検証しましたが、{2} 件の調整はこの PC では利用できないため適用できません。|{0}% 処理済み — {1}/{2}|{0} 件の検証に失敗しました。進行状況ウィンドウで詳細を確認してください。|利用不可
ko|이 PC에서 사용할 수 없음|{1}개 중 {0}개를 검증했지만, {2}개 조정은 이 PC에서 사용할 수 없어 적용할 수 없습니다.|{0}% 처리됨 — {1}/{2}|{0}개 항목 검증에 실패했습니다. 진행 창에서 세부 정보를 확인하세요.|사용 불가
ur|اس پی سی پر دستیاب نہیں|{1} میں سے {0} کی تصدیق ہو گئی، لیکن {2} تبدیلیاں اس پی سی پر دستیاب نہ ہونے کی وجہ سے لاگو نہیں کی جا سکتیں۔|{0}% پر کارروائی ہو گئی — {1}/{2}|{0} آئٹمز کی تصدیق ناکام ہوئی۔ تفصیلات پیش رفت کی ونڈو میں دیکھیں۔|دستیاب نہیں
ta|இந்தக் கணினியில் கிடைக்கவில்லை|{1} இல் {0} சரிபார்க்கப்பட்டன, ஆனால் {2} மாற்றங்கள் இந்தக் கணினியில் கிடைக்காததால் அவற்றைப் பயன்படுத்த முடியாது.|{0}% செயலாக்கப்பட்டது — {1}/{2}|{0} உருப்படிகளின் சரிபார்ப்பு தோல்வியடைந்தது. விவரங்களை முன்னேற்றச் சாளரத்தில் பார்க்கவும்.|கிடைக்கவில்லை
hi|इस पीसी पर उपलब्ध नहीं|{1} में से {0} सत्यापित हुए, लेकिन {2} बदलाव इस पीसी पर उपलब्ध न होने के कारण लागू नहीं किए जा सकते।|{0}% संसाधित — {1}/{2}|{0} आइटमों का सत्यापन विफल हुआ। विवरण प्रगति विंडो में देखें।|अनुपलब्ध
ms|Tidak tersedia pada PC ini|{0} daripada {1} telah disahkan, tetapi {2} pelarasan tidak dapat digunakan kerana tidak tersedia pada PC ini.|{0}% diproses — {1}/{2}|Pengesahan gagal untuk {0} pilihan. Lihat butiran dalam tetingkap kemajuan.|TIDAK TERSEDIA
jv|Ora kasedhiya ing PC iki|{0} saka {1} wis diverifikasi, nanging {2} tweak ora bisa ditrapake amarga ora kasedhiya ing PC iki.|{0}% diproses — {1}/{2}|Verifikasi gagal kanggo {0} opsi. Delengen rincian ing jendhela progres.|ORA KASEDHIYA
ban|Nenten kasayagayang ring PC puniki|{0} saking {1} sampun diverifikasi, nanging {2} tweak nenten prasida kaanggen santukan nenten kasayagayang ring PC puniki.|{0}% kaproses — {1}/{2}|Verifikasi gagal ring {0} opsi. Cingak rincian ring jendela progres.|NENTEN KASAYAGAYANG
sv|Inte tillgängligt på den här datorn|{0} av {1} har verifierats, men {2} justeringar kan inte tillämpas eftersom de inte är tillgängliga på den här datorn.|{0}% bearbetat — {1}/{2}|Verifieringen misslyckades för {0} objekt. Se detaljer i förloppsfönstret.|INTE TILLGÄNGLIGT
es|No disponible en este PC|Se han verificado {0} de {1}, pero no se pueden aplicar {2} ajustes porque no están disponibles en este PC.|{0}% procesado — {1}/{2}|La verificación falló para {0} elementos. Consulta los detalles en la ventana de progreso.|NO DISPONIBLE
""";
}
