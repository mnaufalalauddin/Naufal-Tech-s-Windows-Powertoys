using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

// Editable UTF-8 data, separate from the recovered reference translations.
// Each row must contain exactly one value per English key, for all 23 languages.
internal static partial class NativeUiCatalog
{
    internal static void Merge(Dictionary<string, Dictionary<string, string>> catalog)
    {
        MergeRows(catalog, Controls);
        MergeRows(catalog, Progress);
        MergeRows(catalog, Wizard);
        MergeRows(catalog, WmiWizard);
        MergeRows(catalog, BuiltInApps);
        MergeRows(catalog, AppExpansion);
        MergeRows(catalog, CopilotConsent);
        MergeRows(catalog, OneDriveApps);
        MergeRows(catalog, Monitoring);
        MergeRows(catalog, Status);
        MergeRows(catalog, Safety);
        MergeRows(catalog, Titles);
        MergeRows(catalog, Dashboard);
        MergeRows(catalog, RepairStages);
        MergeRows(catalog, Availability);
        MergeRows(catalog, Workflow);
        MergeRows(catalog, WorkflowResults);
        MergeRows(catalog, GamingDescriptions);
        MergeRows(catalog, WorkflowDialogs);
        MergeRows(catalog, PolicyNotices);
        MergeRows(catalog, RepairConfirmations);
        MergeRows(catalog, RepairOutcomes);
        MergeRows(catalog, RuntimeStatus);
        MergeRows(catalog, RuntimeCompositions);
        MergeRows(catalog, RuntimeAnalysis);
        MergeRows(catalog, CatalogConfirmations);
        MergeRows(catalog, CommonResults);
        MergeRows(catalog, RegistryReadStates);
        MergeRows(catalog, EssentialActionResults);
        MergeRows(catalog, AvailabilityReasons);
        MergeRows(catalog, GamingStatusResults);
        MergeRows(catalog, PrivacyWarnings);
        MergeRows(catalog, AboutContent);
        MergeRows(catalog, PrivacyRemainingDescriptions);
        MergeRows(catalog, AdvancedDescriptions);
        MergeRows(catalog, EssentialRemainingActions);
        MergeRows(catalog, EssentialRemainingTweaks);
        MergeRows(catalog, EssentialLabDescriptions);
        MergeRows(catalog, GamingRemainingConfirmations);
        MergeRows(catalog, GamingRemainingDescriptions);
        MergeRows(catalog, OperationOutcomes);
        MergeRows(catalog, PhotoViewerMessages);
        MergeRows(catalog, EssentialOutcomes);
        MergeRows(catalog, RestoreResults);
        MergeRows(catalog, VerificationStates);
        MergeRows(catalog, ServiceDescriptions);
        MergeRows(catalog, GamingBootDescriptions);
        MergeRows(catalog, GamingLabGpuDescriptions);
        MergeRows(catalog, GamingLabCoreDescriptions);
        MergeRows(catalog, DebloatDetails);
        MergeRows(catalog, DashboardLabels);
        foreach (var item in ReviewedLabelOverrides)
        {
            if (!catalog.TryGetValue(item.Language, out var reviewedTable) || !reviewedTable.ContainsKey(item.Key) ||
                string.IsNullOrWhiteSpace(item.Value))
                throw new InvalidOperationException("Invalid reviewed label override: " + item.Language + " / " + item.Key);
            reviewedTable[item.Key] = item.Value;
        }
        foreach (var table in catalog.Values)
        {
            // Obsolete, unused descriptions disagree on restore semantics.
            // Do not silently reuse the old default-restore wording for snapshots.
            table.Remove("Applies the supplied WPFTweaksServices startup targets and memory-based SvcHostSplitThresholdInKB; OFF restores the exact pre-tweak service snapshot.");
            table.Remove("Applies the supplied WPFTweaksServices startup targets and memory-based SvcHostSplitThresholdInKB; OFF restores the source-defined OriginalType values.");
            foreach (var pair in Aliases) if (table.TryGetValue(pair.Value, out string? text)) table[pair.Key] = text;
            foreach (var pair in WorkflowAliases) table[pair.Key] = table[pair.Value];
        }
    }

    private static void MergeRows(Dictionary<string, Dictionary<string, string>> catalog, string data)
    {
        string[] rows = data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] keys = rows[0].Split('|');
        if (keys[0] != "en" || rows.Length != catalog.Count)
            throw new InvalidOperationException("Native translation table must define all languages, starting with English.");
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> uniqueKeys = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < keys.Length; i++)
            if (string.IsNullOrWhiteSpace(keys[i]) || !uniqueKeys.Add(keys[i]))
                throw new InvalidOperationException("Duplicate or empty native translation key: " + keys[i]);
        foreach (string row in rows)
        {
            string[] cells = row.Split('|');
            if (cells.Length != keys.Length || !languages.Add(cells[0]) || !catalog.TryGetValue(cells[0], out var table))
                throw new InvalidOperationException("Invalid native translation row: " + cells[0]);
            for (int i = 1; i < keys.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(cells[i])) throw new InvalidOperationException("Empty native translation: " + keys[i]);
                table[keys[i]] = cells[i];
            }
        }
    }

    private const string Controls = """
en|Tasks|Active Tasks|Running|Queued|Waiting|Working|Verifying|Completed|Failed|Warning|Skipped|Applying|Restoring|Reading|Collecting|Downloading|Installing|Repairing|Disabling|Enabling|Ending|Suspending|Resuming|Decrypting|Analyzing|Preparing...|OVERALL PROGRESS|LIVE OUTPUT|Copy log|First-Run Setup|RUN SELECTED|DONT SHOW AGAIN|SKIP FOR NOW|RECOMMENDED|FEATURE PREREQUISITE|AUTOMATIC CHECK|COMPATIBILITY|HIGH RISK|HIGH IMPACT|EXPERIMENTAL|SAFE|LEGACY|GENERAL|Restore selected|SELECT A PROFILE|Switch to light mode|Switch to dark mode|Reset to 100%|Text scaling|PROCESSES|UPTIME|RECOVERY|Low|Medium|High|Undefined|Volume|Full Status|Suspend|Resume|Decrypt|Text File
id|Tugas|Tugas Aktif|Berjalan|Dalam antrean|Menunggu|Bekerja|Memverifikasi|Selesai|Gagal|Peringatan|Dilewati|Menerapkan|Memulihkan|Membaca|Mengumpulkan|Mengunduh|Menginstal|Memperbaiki|Menonaktifkan|Mengaktifkan|Mengakhiri|Menangguhkan|Melanjutkan|Mendekripsi|Menganalisis|Menyiapkan...|PROGRES KESELURUHAN|KELUARAN LANGSUNG|Salin log|Persiapan Awal|JALANKAN PILIHAN|JANGAN TAMPILKAN LAGI|LEWATI DULU|DIREKOMENDASIKAN|PRASYARAT FITUR|PEMERIKSAAN OTOMATIS|KOMPATIBILITAS|RISIKO TINGGI|DAMPAK BESAR|EKSPERIMENTAL|AMAN|LAWAS|UMUM|Pulihkan pilihan|PILIH PROFIL|Beralih ke mode terang|Beralih ke mode gelap|Atur ulang ke 100%|Skala teks|PROSES|WAKTU AKTIF|PEMULIHAN|Rendah|Sedang|Tinggi|Tidak ditentukan|Volume|Status Lengkap|Tangguhkan|Lanjutkan|Dekripsi|Berkas Teks
de|Aufgaben|Aktive Aufgaben|Läuft|In Warteschlange|Wartet|In Bearbeitung|Wird überprüft|Abgeschlossen|Fehlgeschlagen|Warnung|Übersprungen|Wird angewendet|Wird wiederhergestellt|Wird gelesen|Wird erfasst|Wird heruntergeladen|Wird installiert|Wird repariert|Wird deaktiviert|Wird aktiviert|Wird beendet|Wird ausgesetzt|Wird fortgesetzt|Wird entschlüsselt|Wird analysiert|Vorbereitung...|GESAMTFORTSCHRITT|LIVE-AUSGABE|Protokoll kopieren|Ersteinrichtung|AUSWAHL AUSFÜHREN|NICHT MEHR ANZEIGEN|VORERST ÜBERSPRINGEN|EMPFOHLEN|FUNKTIONSVORAUSSETZUNG|AUTOMATISCHE PRÜFUNG|KOMPATIBILITÄT|HOHES RISIKO|GROSSE AUSWIRKUNGEN|EXPERIMENTELL|SICHER|VERALTET|ALLGEMEIN|Auswahl wiederherstellen|PROFIL AUSWÄHLEN|Zum hellen Modus wechseln|Zum dunklen Modus wechseln|Auf 100% zurücksetzen|Textskalierung|PROZESSE|BETRIEBSZEIT|WIEDERHERSTELLUNG|Niedrig|Mittel|Hoch|Nicht definiert|Volume|Vollständiger Status|Aussetzen|Fortsetzen|Entschlüsseln|Textdatei
fr|Tâches|Tâches actives|En cours|En attente d'exécution|En attente|Traitement en cours|Vérification en cours|Terminé|Échec|Avertissement|Ignoré|Application en cours|Restauration en cours|Lecture en cours|Collecte en cours|Téléchargement en cours|Installation en cours|Réparation en cours|Désactivation en cours|Activation en cours|Arrêt en cours|Suspension en cours|Reprise en cours|Déchiffrement en cours|Analyse en cours|Préparation...|PROGRESSION GLOBALE|SORTIE EN DIRECT|Copier le journal|Configuration initiale|EXÉCUTER LA SÉLECTION|NE PLUS AFFICHER|IGNORER POUR L'INSTANT|RECOMMANDÉ|PRÉREQUIS DE LA FONCTION|VÉRIFICATION AUTOMATIQUE|COMPATIBILITÉ|RISQUE ÉLEVÉ|IMPACT IMPORTANT|EXPÉRIMENTAL|SÛR|ANCIEN|GÉNÉRAL|Restaurer la sélection|CHOISIR UN PROFIL|Passer au mode clair|Passer au mode sombre|Rétablir à 100%|Échelle du texte|PROCESSUS|DURÉE DE FONCTIONNEMENT|RÉCUPÉRATION|Faible|Moyen|Élevé|Non défini|Volume|État complet|Suspendre|Reprendre|Déchiffrer|Fichier texte
ar|المهام|المهام النشطة|قيد التشغيل|في قائمة الانتظار|بانتظار البدء|جارٍ العمل|جارٍ التحقق|مكتمل|فشل|تحذير|تم التخطي|جارٍ التطبيق|جارٍ الاستعادة|جارٍ القراءة|جارٍ الجمع|جارٍ التنزيل|جارٍ التثبيت|جارٍ الإصلاح|جارٍ التعطيل|جارٍ التمكين|جارٍ الإنهاء|جارٍ التعليق|جارٍ الاستئناف|جارٍ فك التشفير|جارٍ التحليل|جارٍ التحضير...|التقدم الإجمالي|المخرجات المباشرة|نسخ السجل|الإعداد الأولي|تشغيل المحدد|عدم الإظهار مرة أخرى|تخطي الآن|موصى به|متطلب الميزة|فحص تلقائي|التوافق|مخاطر عالية|تأثير كبير|تجريبي|آمن|قديم|عام|استعادة المحدد|اختيار ملف أداء|التبديل إلى الوضع الفاتح|التبديل إلى الوضع الداكن|إعادة الضبط إلى 100%|تحجيم النص|العمليات|وقت التشغيل|الاسترداد|منخفض|متوسط|عالٍ|غير محدد|وحدة التخزين|الحالة الكاملة|تعليق|استئناف|فك التشفير|ملف نصي
tl|Mga Gawain|Mga Aktibong Gawain|Tumatakbo|Nakapila|Naghihintay|Gumagawa|Bineberipika|Tapos na|Nabigo|Babala|Nilaktawan|Inilalapat|Ibinabalik|Binabasa|Kinokolekta|Dina-download|Ini-install|Inaayos|Dini-disable|Ine-enable|Tinatapos|Sinususpinde|Ipinagpapatuloy|Dine-decrypt|Sinusuri|Inihahanda...|KABUUANG PAG-USAD|LIVE NA OUTPUT|Kopyahin ang log|Paunang Pag-setup|PATAKBUHIN ANG PINILI|HUWAG NANG IPAKITA|LAKTAWAN MUNA|INIREREKOMENDA|KINAKAILANGAN NG FEATURE|AWTOMATIKONG PAGSUSURI|PAGIGING TUGMA|MATAAS NA PANGANIB|MALAKING EPEKTO|EKSPERIMENTAL|LIGTAS|LUMA|PANGKALAHATAN|Ibalik ang pinili|PUMILI NG PROFILE|Lumipat sa maliwanag na mode|Lumipat sa madilim na mode|Ibalik sa 100%|Laki ng teksto|MGA PROSESO|TAGAL NG PAGTAKBO|PAGBAWI|Mababa|Katamtaman|Mataas|Hindi tinukoy|Volume|Buong Katayuan|Suspindihin|Ipagpatuloy|I-decrypt|Text File
vi|Tác vụ|Tác vụ đang hoạt động|Đang chạy|Đang xếp hàng|Đang chờ|Đang xử lý|Đang xác minh|Hoàn tất|Thất bại|Cảnh báo|Đã bỏ qua|Đang áp dụng|Đang khôi phục|Đang đọc|Đang thu thập|Đang tải xuống|Đang cài đặt|Đang sửa chữa|Đang vô hiệu hóa|Đang bật|Đang kết thúc|Đang tạm ngưng|Đang tiếp tục|Đang giải mã|Đang phân tích|Đang chuẩn bị...|TIẾN ĐỘ TỔNG THỂ|ĐẦU RA TRỰC TIẾP|Sao chép nhật ký|Thiết lập ban đầu|CHẠY MỤC ĐÃ CHỌN|KHÔNG HIỂN THỊ LẠI|TẠM BỎ QUA|KHUYẾN NGHỊ|ĐIỀU KIỆN TIÊN QUYẾT|KIỂM TRA TỰ ĐỘNG|TƯƠNG THÍCH|RỦI RO CAO|TÁC ĐỘNG LỚN|THỬ NGHIỆM|AN TOÀN|CŨ|CHUNG|Khôi phục mục đã chọn|CHỌN CẤU HÌNH|Chuyển sang chế độ sáng|Chuyển sang chế độ tối|Đặt lại về 100%|Tỷ lệ văn bản|TIẾN TRÌNH|THỜI GIAN HOẠT ĐỘNG|KHÔI PHỤC|Thấp|Trung bình|Cao|Chưa xác định|Ổ đĩa|Trạng thái đầy đủ|Tạm ngưng|Tiếp tục|Giải mã|Tệp văn bản
zh-CN|任务|活动任务|正在运行|已排队|正在等待|正在处理|正在验证|已完成|失败|警告|已跳过|正在应用|正在还原|正在读取|正在收集|正在下载|正在安装|正在修复|正在禁用|正在启用|正在结束|正在暂停|正在恢复|正在解密|正在分析|正在准备...|总体进度|实时输出|复制日志|首次设置|运行所选项|不再显示|暂时跳过|推荐|功能先决条件|自动检查|兼容性|高风险|高影响|实验性|安全|旧版|常规|还原所选项|选择配置|切换到浅色模式|切换到深色模式|重置为100%|文本缩放|进程|运行时间|恢复|低|中|高|未定义|卷|完整状态|暂停|恢复|解密|文本文件
zh-TW|工作|進行中的工作|正在執行|已排入佇列|正在等待|正在處理|正在驗證|已完成|失敗|警告|已略過|正在套用|正在還原|正在讀取|正在收集|正在下載|正在安裝|正在修復|正在停用|正在啟用|正在結束|正在暫停|正在繼續|正在解密|正在分析|正在準備...|整體進度|即時輸出|複製記錄|首次設定|執行選取項目|不再顯示|暫時略過|建議|功能必要條件|自動檢查|相容性|高風險|高影響|實驗性|安全|舊版|一般|還原選取項目|選擇設定檔|切換至淺色模式|切換至深色模式|重設為100%|文字縮放|處理程序|運作時間|復原|低|中|高|未定義|磁碟區|完整狀態|暫停|繼續|解密|文字檔案
th|งาน|งานที่กำลังทำงาน|กำลังทำงาน|อยู่ในคิว|กำลังรอ|กำลังดำเนินการ|กำลังตรวจสอบ|เสร็จสิ้น|ล้มเหลว|คำเตือน|ข้ามแล้ว|กำลังนำไปใช้|กำลังกู้คืน|กำลังอ่าน|กำลังรวบรวม|กำลังดาวน์โหลด|กำลังติดตั้ง|กำลังซ่อมแซม|กำลังปิดใช้งาน|กำลังเปิดใช้งาน|กำลังสิ้นสุด|กำลังระงับ|กำลังดำเนินการต่อ|กำลังถอดรหัส|กำลังวิเคราะห์|กำลังเตรียม...|ความคืบหน้าโดยรวม|ผลลัพธ์สด|คัดลอกบันทึก|การตั้งค่าครั้งแรก|เรียกใช้รายการที่เลือก|ไม่ต้องแสดงอีก|ข้ามก่อน|แนะนำ|ข้อกำหนดของคุณสมบัติ|ตรวจสอบอัตโนมัติ|ความเข้ากันได้|ความเสี่ยงสูง|ผลกระทบสูง|ทดลอง|ปลอดภัย|รุ่นเก่า|ทั่วไป|กู้คืนรายการที่เลือก|เลือกโปรไฟล์|เปลี่ยนเป็นโหมดสว่าง|เปลี่ยนเป็นโหมดมืด|รีเซ็ตเป็น 100%|มาตราส่วนข้อความ|กระบวนการ|เวลาที่ทำงาน|การกู้คืน|ต่ำ|ปานกลาง|สูง|ไม่ได้กำหนด|โวลุม|สถานะทั้งหมด|ระงับ|ดำเนินการต่อ|ถอดรหัส|ไฟล์ข้อความ
ru|Задачи|Активные задачи|Выполняется|В очереди|Ожидание|Обработка|Проверка|Завершено|Ошибка|Предупреждение|Пропущено|Применение|Восстановление|Чтение|Сбор|Загрузка|Установка|Исправление|Отключение|Включение|Завершение|Приостановка|Возобновление|Расшифровка|Анализ|Подготовка...|ОБЩИЙ ХОД ВЫПОЛНЕНИЯ|ТЕКУЩИЙ ВЫВОД|Копировать журнал|Начальная настройка|ЗАПУСТИТЬ ВЫБРАННОЕ|БОЛЬШЕ НЕ ПОКАЗЫВАТЬ|ПОКА ПРОПУСТИТЬ|РЕКОМЕНДУЕТСЯ|НЕОБХОДИМЫЙ КОМПОНЕНТ|АВТОМАТИЧЕСКАЯ ПРОВЕРКА|СОВМЕСТИМОСТЬ|ВЫСОКИЙ РИСК|СИЛЬНОЕ ВЛИЯНИЕ|ЭКСПЕРИМЕНТАЛЬНОЕ|БЕЗОПАСНОЕ|УСТАРЕВШЕЕ|ОБЩЕЕ|Восстановить выбранное|ВЫБЕРИТЕ ПРОФИЛЬ|Перейти в светлый режим|Перейти в тёмный режим|Сбросить до 100%|Масштаб текста|ПРОЦЕССЫ|ВРЕМЯ РАБОТЫ|ВОССТАНОВЛЕНИЕ|Низкий|Средний|Высокий|Не определено|Том|Полный статус|Приостановить|Возобновить|Расшифровать|Текстовый файл
uk|Завдання|Активні завдання|Виконується|У черзі|Очікування|Обробка|Перевірка|Завершено|Помилка|Попередження|Пропущено|Застосування|Відновлення|Читання|Збирання|Завантаження|Установлення|Виправлення|Вимкнення|Увімкнення|Завершення|Призупинення|Продовження|Розшифрування|Аналіз|Підготовка...|ЗАГАЛЬНИЙ ПЕРЕБІГ|ПОТОЧНИЙ ВИВІД|Копіювати журнал|Початкове налаштування|ЗАПУСТИТИ ВИБРАНЕ|БІЛЬШЕ НЕ ПОКАЗУВАТИ|ПОКИ ПРОПУСТИТИ|РЕКОМЕНДОВАНО|НЕОБХІДНИЙ КОМПОНЕНТ|АВТОМАТИЧНА ПЕРЕВІРКА|СУМІСНІСТЬ|ВИСОКИЙ РИЗИК|ЗНАЧНИЙ ВПЛИВ|ЕКСПЕРИМЕНТАЛЬНЕ|БЕЗПЕЧНЕ|ЗАСТАРІЛЕ|ЗАГАЛЬНЕ|Відновити вибране|ВИБЕРІТЬ ПРОФІЛЬ|Перейти до світлого режиму|Перейти до темного режиму|Скинути до 100%|Масштаб тексту|ПРОЦЕСИ|ЧАС РОБОТИ|ВІДНОВЛЕННЯ|Низький|Середній|Високий|Не визначено|Том|Повний стан|Призупинити|Продовжити|Розшифрувати|Текстовий файл
pt|Tarefas|Tarefas ativas|Em execução|Em fila|A aguardar|A processar|A verificar|Concluído|Falhou|Aviso|Ignorado|A aplicar|A restaurar|A ler|A recolher|A transferir|A instalar|A reparar|A desativar|A ativar|A terminar|A suspender|A retomar|A desencriptar|A analisar|A preparar...|PROGRESSO GLOBAL|SAÍDA EM DIRETO|Copiar registo|Configuração inicial|EXECUTAR SELEÇÃO|NÃO VOLTAR A MOSTRAR|IGNORAR POR AGORA|RECOMENDADO|PRÉ-REQUISITO DA FUNÇÃO|VERIFICAÇÃO AUTOMÁTICA|COMPATIBILIDADE|RISCO ELEVADO|IMPACTO ELEVADO|EXPERIMENTAL|SEGURO|ANTIGO|GERAL|Restaurar seleção|SELECIONAR PERFIL|Mudar para o modo claro|Mudar para o modo escuro|Repor a 100%|Escala do texto|PROCESSOS|TEMPO DE ATIVIDADE|RECUPERAÇÃO|Baixo|Médio|Alto|Não definido|Volume|Estado completo|Suspender|Retomar|Desencriptar|Ficheiro de texto
ja|タスク|実行中のタスク|実行中|待機列に登録済み|待機中|処理中|検証中|完了|失敗|警告|スキップ済み|適用中|復元中|読み取り中|収集中|ダウンロード中|インストール中|修復中|無効化中|有効化中|終了中|中断中|再開中|暗号化解除中|分析中|準備中...|全体の進行状況|リアルタイム出力|ログをコピー|初回セットアップ|選択項目を実行|今後表示しない|今はスキップ|推奨|機能の前提条件|自動チェック|互換性|高リスク|影響大|試験的|安全|旧式|一般|選択項目を復元|プロファイルを選択|ライトモードに切り替え|ダークモードに切り替え|100%にリセット|テキストの拡大率|プロセス|稼働時間|回復|低|中|高|未定義|ボリューム|詳細な状態|中断|再開|暗号化解除|テキストファイル
ko|작업|활성 작업|실행 중|대기열에 있음|대기 중|처리 중|검증 중|완료|실패|경고|건너뜀|적용 중|복원 중|읽는 중|수집 중|다운로드 중|설치 중|복구 중|비활성화 중|활성화 중|종료 중|일시 중단 중|다시 시작 중|암호 해독 중|분석 중|준비 중...|전체 진행률|실시간 출력|로그 복사|초기 설정|선택 항목 실행|다시 표시하지 않음|지금은 건너뛰기|권장|기능 필수 조건|자동 검사|호환성|높은 위험|큰 영향|실험적|안전|레거시|일반|선택 항목 복원|프로필 선택|라이트 모드로 전환|다크 모드로 전환|100%로 초기화|텍스트 배율|프로세스|가동 시간|복구|낮음|중간|높음|정의되지 않음|볼륨|전체 상태|일시 중단|다시 시작|암호 해독|텍스트 파일
ur|کام|فعال کام|چل رہا ہے|قطار میں|انتظار میں|کام جاری ہے|تصدیق جاری ہے|مکمل|ناکام|انتباہ|چھوڑ دیا گیا|لاگو کیا جا رہا ہے|بحال کیا جا رہا ہے|پڑھا جا رہا ہے|جمع کیا جا رہا ہے|ڈاؤن لوڈ ہو رہا ہے|تنصیب جاری ہے|مرمت جاری ہے|غیر فعال کیا جا رہا ہے|فعال کیا جا رہا ہے|ختم کیا جا رہا ہے|معطل کیا جا رہا ہے|دوبارہ جاری کیا جا رہا ہے|خفیہ کاری ہٹائی جا رہی ہے|تجزیہ جاری ہے|تیاری جاری ہے...|مجموعی پیش رفت|براہ راست آؤٹ پٹ|لاگ نقل کریں|ابتدائی ترتیب|منتخب کام چلائیں|دوبارہ نہ دکھائیں|ابھی چھوڑ دیں|تجویز کردہ|خصوصیت کی پیشگی شرط|خودکار جانچ|مطابقت|زیادہ خطرہ|زیادہ اثر|تجرباتی|محفوظ|پرانا|عمومی|منتخب بحال کریں|پروفائل منتخب کریں|روشن موڈ پر جائیں|تاریک موڈ پر جائیں|100% پر واپس کریں|متن کا پیمانہ|عمل|چلنے کا دورانیہ|بحالی|کم|درمیانہ|زیادہ|غیر متعین|والیوم|مکمل حالت|معطل کریں|دوبارہ جاری کریں|خفیہ کاری ہٹائیں|متنی فائل
ta|பணிகள்|செயலில் உள்ள பணிகள்|இயங்குகிறது|வரிசையில் உள்ளது|காத்திருக்கிறது|செயல்படுகிறது|சரிபார்க்கிறது|முடிந்தது|தோல்வி|எச்சரிக்கை|தவிர்க்கப்பட்டது|பயன்படுத்துகிறது|மீட்டமைக்கிறது|படிக்கிறது|சேகரிக்கிறது|பதிவிறக்குகிறது|நிறுவுகிறது|பழுதுபார்க்கிறது|முடக்குகிறது|இயக்குகிறது|முடிக்கிறது|இடைநிறுத்துகிறது|தொடர்கிறது|மறைகுறியாக்கத்தை நீக்குகிறது|ஆய்வு செய்கிறது|தயாராகிறது...|ஒட்டுமொத்த முன்னேற்றம்|நேரடி வெளியீடு|பதிவை நகலெடு|முதல் முறை அமைப்பு|தேர்ந்தெடுத்தவற்றை இயக்கு|மீண்டும் காட்ட வேண்டாம்|இப்போதைக்கு தவிர்|பரிந்துரைக்கப்படுகிறது|அம்சத்திற்கான முன்தேவை|தானியங்கிச் சரிபார்ப்பு|இணக்கத்தன்மை|அதிக ஆபத்து|அதிக தாக்கம்|சோதனைக்குரியது|பாதுகாப்பானது|பழையது|பொது|தேர்ந்தெடுத்தவற்றை மீட்டமை|சுயவிவரத்தைத் தேர்ந்தெடு|வெளிர் முறைக்கு மாறு|இருண்ட முறைக்கு மாறு|100%க்கு மீட்டமை|உரை அளவிடல்|செயல்முறைகள்|இயங்கிய நேரம்|மீட்பு|குறைவு|நடுத்தரம்|அதிகம்|வரையறுக்கப்படவில்லை|தொகுதி|முழு நிலை|இடைநிறுத்து|தொடர்|மறைகுறியாக்கத்தை நீக்கு|உரை கோப்பு
hi|कार्य|सक्रिय कार्य|चल रहा है|कतार में|प्रतीक्षा में|काम जारी है|सत्यापन जारी है|पूरा हुआ|विफल|चेतावनी|छोड़ा गया|लागू हो रहा है|बहाल हो रहा है|पढ़ा जा रहा है|एकत्र हो रहा है|डाउनलोड हो रहा है|इंस्टॉल हो रहा है|मरम्मत जारी है|अक्षम हो रहा है|सक्षम हो रहा है|समाप्त हो रहा है|निलंबित हो रहा है|फिर शुरू हो रहा है|डिक्रिप्ट हो रहा है|विश्लेषण जारी है|तैयारी जारी है...|कुल प्रगति|लाइव आउटपुट|लॉग कॉपी करें|प्रारंभिक सेटअप|चयनित कार्य चलाएँ|फिर न दिखाएँ|अभी छोड़ें|अनुशंसित|सुविधा की पूर्वापेक्षा|स्वचालित जाँच|अनुकूलता|अधिक जोखिम|अधिक प्रभाव|प्रयोगात्मक|सुरक्षित|पुराना|सामान्य|चयनित बहाल करें|प्रोफ़ाइल चुनें|हल्के मोड पर जाएँ|गहरे मोड पर जाएँ|100% पर रीसेट करें|टेक्स्ट स्केलिंग|प्रक्रियाएँ|चलने का समय|पुनर्प्राप्ति|कम|मध्यम|अधिक|अपरिभाषित|वॉल्यूम|पूरी स्थिति|निलंबित करें|फिर शुरू करें|डिक्रिप्ट करें|टेक्स्ट फ़ाइल
ms|Tugas|Tugas Aktif|Sedang berjalan|Dalam baris gilir|Menunggu|Sedang diproses|Sedang mengesahkan|Selesai|Gagal|Amaran|Dilangkau|Sedang menggunakan|Sedang memulihkan|Sedang membaca|Sedang mengumpulkan|Sedang memuat turun|Sedang memasang|Sedang membaiki|Sedang melumpuhkan|Sedang mendayakan|Sedang menamatkan|Sedang menggantung|Sedang menyambung|Sedang menyahsulit|Sedang menganalisis|Sedang menyediakan...|KEMAJUAN KESELURUHAN|OUTPUT LANGSUNG|Salin log|Persediaan Awal|JALANKAN PILIHAN|JANGAN PAPARKAN LAGI|LANGKAU DAHULU|DISYORKAN|PRASYARAT CIRI|SEMAKAN AUTOMATIK|KESERASIAN|RISIKO TINGGI|IMPAK TINGGI|PERCUBAAN|SELAMAT|LEGASI|UMUM|Pulihkan pilihan|PILIH PROFIL|Tukar ke mod cerah|Tukar ke mod gelap|Tetapkan semula ke 100%|Penskalaan teks|PROSES|MASA AKTIF|PEMULIHAN|Rendah|Sederhana|Tinggi|Tidak ditakrifkan|Volum|Status Penuh|Gantung|Sambung|Nyahsulit|Fail Teks
jv|Tugas|Tugas Aktif|Mlaku|Ing antrean|Ngenteni|Lagi makarya|Lagi mriksa|Rampung|Gagal|Pènget|Diliwati|Lagi nerapake|Lagi mulihake|Lagi maca|Lagi nglumpukake|Lagi ngundhuh|Lagi nginstal|Lagi ndandani|Lagi mateni|Lagi nguripake|Lagi mungkasi|Lagi nundha|Lagi nerusake|Lagi mbukak enkripsi|Lagi nganalisis|Lagi nyiapake...|PROGRES SAKABÈHÉ|OUTPUT LANGSUNG|Salin log|Persiyapan Wiwitan|LAKOKAKE PILIHAN|AJA DITAMPILAKE MANEH|LIWATI DHISIK|DIANJURAKE|PRASYARAT FITUR|PAMRIKSAN OTOMATIS|KOMPATIBILITAS|RISIKO DHUWUR|DAMPAK GEDHÉ|EKSPERIMENTAL|AMAN|LAWAS|UMUM|Pulihake pilihan|PILIH PROFIL|Ganti menyang mode padhang|Ganti menyang mode peteng|Balekake menyang 100%|Skala teks|PROSES|SUWÉ AKTIF|PAMULIHAN|Cendhèk|Sedheng|Dhuwur|Ora ditemtokake|Volume|Status Jangkep|Tundha|Terusake|Bukak enkripsi|Berkas Teks
ban|Tugas|Tugas Aktif|Majalan|Ring antrean|Ngantos|Sedeng makarya|Sedeng mriksa|Sampun puput|Gagal|Pangeling|Kalintang|Sedeng nerapang|Sedeng mulihang|Sedeng maca|Sedeng ngempulang|Sedeng ngunduh|Sedeng nginstal|Sedeng ngaryanin becik|Sedeng matiang|Sedeng nguripang|Sedeng muputang|Sedeng nundang|Sedeng nglanturang|Sedeng ngicalang enkripsi|Sedeng nganalisis|Sedeng nyiapang...|PROGRES SAMI|OUTPUT LANGSUNG|Salin log|Persiapan Kawitan|JALANANG PILIHAN|SAMPUN KATAMPILANG MALIH|LINTANGANG DUMUN|KASARANANG|PRASYARAT FITUR|PAMARIKSAAN OTOMATIS|KOMPATIBILITAS|RISIKO TEGEH|DAMPAK AGENG|EKSPERIMENTAL|AMAN|LAWAS|UMUM|Pulihang pilihan|PILIH PROFIL|Gentos ka mode galang|Gentos ka mode peteng|Waliang ka 100%|Skala teks|PROSES|DURASI AKTIF|PAMULIHAN|Andap|Sedeng|Tegeh|Durung katentuang|Volume|Status Jangkep|Tunda|Lanturang|Icalang enkripsi|Berkas Teks
sv|Uppgifter|Aktiva uppgifter|Körs|Köad|Väntar|Arbetar|Verifierar|Slutförd|Misslyckades|Varning|Överhoppad|Tillämpar|Återställer|Läser|Samlar in|Hämtar|Installerar|Reparerar|Inaktiverar|Aktiverar|Avslutar|Pausar|Återupptar|Dekrypterar|Analyserar|Förbereder...|TOTALT FÖRLOPP|LIVEUTDATA|Kopiera logg|Första konfigurationen|KÖR VALDA|VISA INTE IGEN|HOPPA ÖVER NU|REKOMMENDERAT|FUNKTIONSKRAV|AUTOMATISK KONTROLL|KOMPATIBILITET|HÖG RISK|STOR PÅVERKAN|EXPERIMENTELL|SÄKER|ÄLDRE|ALLMÄNT|Återställ valda|VÄLJ PROFIL|Växla till ljust läge|Växla till mörkt läge|Återställ till 100%|Textskalning|PROCESSER|DRIFTTID|ÅTERSTÄLLNING|Låg|Medel|Hög|Odefinierad|Volym|Fullständig status|Pausa|Återuppta|Dekryptera|Textfil
es|Tareas|Tareas activas|En ejecución|En cola|En espera|Procesando|Verificando|Completado|Falló|Advertencia|Omitido|Aplicando|Restaurando|Leyendo|Recopilando|Descargando|Instalando|Reparando|Desactivando|Activando|Finalizando|Suspendiendo|Reanudando|Descifrando|Analizando|Preparando...|PROGRESO GENERAL|SALIDA EN DIRECTO|Copiar registro|Configuración inicial|EJECUTAR SELECCIÓN|NO VOLVER A MOSTRAR|OMITIR POR AHORA|RECOMENDADO|REQUISITO DE LA FUNCIÓN|COMPROBACIÓN AUTOMÁTICA|COMPATIBILIDAD|ALTO RIESGO|ALTO IMPACTO|EXPERIMENTAL|SEGURO|ANTIGUO|GENERAL|Restaurar selección|SELECCIONAR PERFIL|Cambiar al modo claro|Cambiar al modo oscuro|Restablecer al 100%|Escala del texto|PROCESOS|TIEMPO DE ACTIVIDAD|RECUPERACIÓN|Bajo|Medio|Alto|Sin definir|Volumen|Estado completo|Suspender|Reanudar|Descifrar|Archivo de texto
""";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        // Languages has a dedicated reviewed resource; the recovered LANGUAGE
        // alias contains English copies and must not overwrite that resource.
        ["GAME MODE"] = "GAME MODE STATUS", ["Disk Information"] = "Disk Info",
        ["Games Runtime & Compatibility Check"] = "Games Runtime & Compatibility Check",
        ["Games Runtime & Compatibility"] = "Games Runtime & Compatibility Check",
        ["Copy log"] = "Copy log", ["Waiting to start"] = "Waiting to start.",
        ["Not verified"] = "NOT VERIFIED", ["PASS"] = "VERIFIED", ["COMPLETE"] = "Completed",
        ["The operation failed."] = "Operation failed.", ["Apply changes"] = "Apply selected",
        ["Refresh devices"] = "Refresh", ["Refresh inventory"] = "Refresh", ["Repair driver"] = "REPAIR",
        ["Run Repair"] = "REPAIR"
    };

    private const string Progress = """
en|{0}% complete — {1}/{2}|{0}% processed — {1}/{2}; errors or unverified items|Windows step: {0}% — Working|{0} of {1}|Failed — see details|Skipped — not started|Waiting to start.|Verifying — waiting for Windows|Working — waiting for Windows|Completed and verified.|Finished with errors|Elapsed: {0}|Updated {0}|Time spent {0}|Text scaling: {0}%
id|{0}% selesai — {1}/{2}|{0}% diproses — {1}/{2}; ada kesalahan atau item belum terverifikasi|Tahap Windows: {0}% — Bekerja|{0} dari {1}|Gagal — lihat detail|Dilewati — belum dimulai|Menunggu untuk dimulai.|Memverifikasi — menunggu Windows|Bekerja — menunggu Windows|Selesai dan terverifikasi.|Selesai dengan kesalahan|Waktu berjalan: {0}|Diperbarui {0}|Waktu digunakan {0}|Skala teks: {0}%
de|{0}% abgeschlossen — {1}/{2}|{0}% verarbeitet — {1}/{2}; Fehler oder ungeprüfte Elemente|Windows-Schritt: {0}% — In Bearbeitung|{0} von {1}|Fehlgeschlagen — siehe Details|Übersprungen — nicht gestartet|Warten auf Start.|Überprüfung — auf Windows warten|In Bearbeitung — auf Windows warten|Abgeschlossen und überprüft.|Mit Fehlern beendet|Verstrichen: {0}|Aktualisiert {0}|Verwendete Zeit {0}|Textskalierung: {0}%
fr|{0}% terminé — {1}/{2}|{0}% traité — {1}/{2} ; erreurs ou éléments non vérifiés|Étape Windows : {0}% — Traitement en cours|{0} sur {1}|Échec — voir les détails|Ignoré — non démarré|En attente du démarrage.|Vérification — en attente de Windows|Traitement — en attente de Windows|Terminé et vérifié.|Terminé avec des erreurs|Temps écoulé : {0}|Mis à jour {0}|Temps utilisé {0}|Échelle du texte : {0}%
ar|اكتمل {0}% — {1}/{2}|تمت معالجة {0}% — {1}/{2}؛ أخطاء أو عناصر غير متحقق منها|خطوة Windows: {0}% — جارٍ العمل|{0} من {1}|فشل — راجع التفاصيل|تم التخطي — لم يبدأ|بانتظار البدء.|جارٍ التحقق — بانتظار Windows|جارٍ العمل — بانتظار Windows|اكتمل وتم التحقق.|انتهى مع أخطاء|الوقت المنقضي: {0}|تم التحديث {0}|الوقت المستغرق {0}|تحجيم النص: {0}%
tl|{0}% tapos — {1}/{2}|{0}% naproseso — {1}/{2}; may error o hindi pa beripikadong item|Hakbang ng Windows: {0}% — Gumagawa|{0} sa {1}|Nabigo — tingnan ang detalye|Nilaktawan — hindi nasimulan|Naghihintay na magsimula.|Bineberipika — naghihintay sa Windows|Gumagawa — naghihintay sa Windows|Tapos na at napatunayan.|Natapos na may mga error|Lumipas: {0}|Na-update {0}|Oras na ginamit {0}|Laki ng teksto: {0}%
vi|Hoàn tất {0}% — {1}/{2}|Đã xử lý {0}% — {1}/{2}; có lỗi hoặc mục chưa xác minh|Bước Windows: {0}% — Đang xử lý|{0} trên {1}|Thất bại — xem chi tiết|Đã bỏ qua — chưa bắt đầu|Đang chờ bắt đầu.|Đang xác minh — chờ Windows|Đang xử lý — chờ Windows|Đã hoàn tất và xác minh.|Kết thúc với lỗi|Thời gian đã qua: {0}|Cập nhật {0}|Thời gian sử dụng {0}|Tỷ lệ văn bản: {0}%
zh-CN|已完成 {0}% — {1}/{2}|已处理 {0}% — {1}/{2}；存在错误或未验证的项目|Windows 步骤：{0}% — 正在处理|第 {0} 项，共 {1} 项|失败 — 查看详细信息|已跳过 — 未开始|正在等待开始。|正在验证 — 等待 Windows|正在处理 — 等待 Windows|已完成并验证。|完成，但存在错误|已用时间：{0}|更新于 {0}|使用时间 {0}|文本缩放：{0}%
zh-TW|已完成 {0}% — {1}/{2}|已處理 {0}% — {1}/{2}；存在錯誤或未驗證的項目|Windows 步驟：{0}% — 正在處理|第 {0} 項，共 {1} 項|失敗 — 檢視詳細資料|已略過 — 尚未開始|正在等待開始。|正在驗證 — 等待 Windows|正在處理 — 等待 Windows|已完成並驗證。|完成，但發生錯誤|已用時間：{0}|更新於 {0}|使用時間 {0}|文字縮放：{0}%
th|เสร็จแล้ว {0}% — {1}/{2}|ประมวลผลแล้ว {0}% — {1}/{2}; มีข้อผิดพลาดหรือรายการที่ยังไม่ผ่านการตรวจสอบ|ขั้นตอน Windows: {0}% — กำลังดำเนินการ|{0} จาก {1}|ล้มเหลว — ดูรายละเอียด|ข้ามแล้ว — ยังไม่เริ่ม|กำลังรอเริ่มต้น|กำลังตรวจสอบ — รอ Windows|กำลังดำเนินการ — รอ Windows|เสร็จสิ้นและตรวจสอบแล้ว|เสร็จสิ้นพร้อมข้อผิดพลาด|เวลาที่ผ่านไป: {0}|อัปเดต {0}|เวลาที่ใช้ {0}|มาตราส่วนข้อความ: {0}%
ru|Выполнено {0}% — {1}/{2}|Обработано {0}% — {1}/{2}; ошибки или непроверенные элементы|Этап Windows: {0}% — Обработка|{0} из {1}|Ошибка — см. подробности|Пропущено — не запущено|Ожидание запуска.|Проверка — ожидание Windows|Обработка — ожидание Windows|Завершено и проверено.|Завершено с ошибками|Прошло: {0}|Обновлено {0}|Затрачено времени {0}|Масштаб текста: {0}%
uk|Виконано {0}% — {1}/{2}|Оброблено {0}% — {1}/{2}; помилки або неперевірені елементи|Етап Windows: {0}% — Обробка|{0} із {1}|Помилка — див. подробиці|Пропущено — не запущено|Очікування запуску.|Перевірка — очікування Windows|Обробка — очікування Windows|Завершено й перевірено.|Завершено з помилками|Минуло: {0}|Оновлено {0}|Витрачено часу {0}|Масштаб тексту: {0}%
pt|{0}% concluído — {1}/{2}|{0}% processado — {1}/{2}; erros ou itens não verificados|Etapa Windows: {0}% — A processar|{0} de {1}|Falhou — consultar detalhes|Ignorado — não iniciado|A aguardar o início.|A verificar — a aguardar o Windows|A processar — a aguardar o Windows|Concluído e verificado.|Terminado com erros|Tempo decorrido: {0}|Atualizado {0}|Tempo utilizado {0}|Escala do texto: {0}%
ja|{0}% 完了 — {1}/{2}|{0}% 処理済み — {1}/{2}；エラーまたは未検証の項目あり|Windows の処理：{0}% — 処理中|全 {1} 件中 {0} 件|失敗 — 詳細を確認|スキップ済み — 未開始|開始を待機中。|検証中 — Windows の応答待ち|処理中 — Windows の応答待ち|完了・検証済み。|エラーありで終了|経過時間：{0}|更新時刻 {0}|使用時間 {0}|テキストの拡大率：{0}%
ko|{0}% 완료 — {1}/{2}|{0}% 처리됨 — {1}/{2}; 오류 또는 미검증 항목 있음|Windows 단계: {0}% — 처리 중|전체 {1}개 중 {0}개|실패 — 세부 정보 확인|건너뜀 — 시작되지 않음|시작 대기 중.|검증 중 — Windows 응답 대기|처리 중 — Windows 응답 대기|완료 및 검증됨.|오류와 함께 종료됨|경과 시간: {0}|업데이트 {0}|사용 시간 {0}|텍스트 배율: {0}%
ur|{0}% مکمل — {1}/{2}|{0}% پر کارروائی ہوئی — {1}/{2}؛ غلطیاں یا غیر تصدیق شدہ اشیا|Windows مرحلہ: {0}% — کام جاری ہے|{1} میں سے {0}|ناکام — تفصیلات دیکھیں|چھوڑ دیا گیا — شروع نہیں ہوا|شروع ہونے کا انتظار ہے۔|تصدیق جاری ہے — Windows کا انتظار|کام جاری ہے — Windows کا انتظار|مکمل اور تصدیق شدہ۔|غلطیوں کے ساتھ ختم ہوا|گزرا وقت: {0}|تازہ کاری {0}|استعمال شدہ وقت {0}|متن کا پیمانہ: {0}%
ta|{0}% முடிந்தது — {1}/{2}|{0}% செயலாக்கப்பட்டது — {1}/{2}; பிழைகள் அல்லது சரிபார்க்கப்படாத உருப்படிகள்|Windows படி: {0}% — செயல்படுகிறது|{1} இல் {0}|தோல்வி — விவரங்களைப் பார்க்கவும்|தவிர்க்கப்பட்டது — தொடங்கவில்லை|தொடங்கக் காத்திருக்கிறது.|சரிபார்க்கிறது — Windowsக்காகக் காத்திருக்கிறது|செயல்படுகிறது — Windowsக்காகக் காத்திருக்கிறது|முடிந்தது, சரிபார்க்கப்பட்டது.|பிழைகளுடன் முடிந்தது|கடந்த நேரம்: {0}|புதுப்பிக்கப்பட்டது {0}|பயன்படுத்திய நேரம் {0}|உரை அளவிடல்: {0}%
hi|{0}% पूरा — {1}/{2}|{0}% संसाधित — {1}/{2}; त्रुटियाँ या असत्यापित आइटम|Windows चरण: {0}% — काम जारी है|{1} में से {0}|विफल — विवरण देखें|छोड़ा गया — शुरू नहीं हुआ|शुरू होने की प्रतीक्षा में।|सत्यापन जारी है — Windows की प्रतीक्षा|काम जारी है — Windows की प्रतीक्षा|पूरा और सत्यापित।|त्रुटियों के साथ समाप्त|बीता समय: {0}|अपडेट किया गया {0}|इस्तेमाल हुआ समय {0}|टेक्स्ट स्केलिंग: {0}%
ms|{0}% selesai — {1}/{2}|{0}% diproses — {1}/{2}; ralat atau item belum disahkan|Langkah Windows: {0}% — Sedang diproses|{0} daripada {1}|Gagal — lihat butiran|Dilangkau — belum bermula|Menunggu untuk bermula.|Sedang mengesahkan — menunggu Windows|Sedang diproses — menunggu Windows|Selesai dan disahkan.|Selesai dengan ralat|Masa berlalu: {0}|Dikemas kini {0}|Masa digunakan {0}|Penskalaan teks: {0}%
jv|{0}% rampung — {1}/{2}|{0}% diproses — {1}/{2}; ana kasalahan utawa item durung dipriksa|Tahap Windows: {0}% — Lagi makarya|{0} saka {1}|Gagal — delengen rincian|Diliwati — durung diwiwiti|Ngenteni diwiwiti.|Lagi mriksa — ngenteni Windows|Lagi makarya — ngenteni Windows|Rampung lan wis dipriksa.|Rampung kanthi kasalahan|Wektu lumaku: {0}|Dianyari {0}|Wektu sing digunakake {0}|Skala teks: {0}%
ban|{0}% puput — {1}/{2}|{0}% kaproses — {1}/{2}; wenten kasalahan utawi item durung kapariksa|Tahap Windows: {0}% — Sedeng makarya|{0} saking {1}|Gagal — cingakin rincian|Kalintang — durung kawitin|Ngantos kawitin.|Sedeng mriksa — ngantos Windows|Sedeng makarya — ngantos Windows|Sampun puput lan kapariksa.|Puput sareng kasalahan|Waktu majalan: {0}|Kaanyarang {0}|Waktu sane kaanggen {0}|Skala teks: {0}%
sv|{0}% klart — {1}/{2}|{0}% bearbetat — {1}/{2}; fel eller overifierade objekt|Windows-steg: {0}% — Arbetar|{0} av {1}|Misslyckades — se detaljer|Överhoppad — inte startad|Väntar på att starta.|Verifierar — väntar på Windows|Arbetar — väntar på Windows|Slutfört och verifierat.|Avslutat med fel|Förfluten tid: {0}|Uppdaterat {0}|Använd tid {0}|Textskalning: {0}%
es|{0}% completado — {1}/{2}|{0}% procesado — {1}/{2}; errores o elementos sin verificar|Paso de Windows: {0}% — Procesando|{0} de {1}|Falló — ver los detalles|Omitido — no iniciado|Esperando para comenzar.|Verificando — esperando a Windows|Procesando — esperando a Windows|Completado y verificado.|Finalizado con errores|Tiempo transcurrido: {0}|Actualizado {0}|Tiempo utilizado {0}|Escala del texto: {0}%
""";
}
