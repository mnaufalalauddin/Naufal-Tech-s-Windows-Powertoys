param([string]$Reference = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'V78.ps1'))
# Data-only extraction: SafeGetValue accepts literal AST data, never executes the script.
$ErrorActionPreference = 'Stop'
$tokens=$null; $errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($Reference,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Reference has parse errors.'}
$tables=@{}
foreach($node in $ast.FindAll({param($n) $n -is [System.Management.Automation.Language.HashtableAst]},$true)){
    try { $data=$node.SafeGetValue() } catch { continue }
    if($data -isnot [Collections.IDictionary] -or -not $data.Contains('en') -or $data['en'] -isnot [Collections.IDictionary]){continue}
    foreach($language in $data.Keys){
        if(-not $tables.ContainsKey($language)){$tables[$language]=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)}
        foreach($key in $data['en'].Keys){
            if($data['en'][$key] -is [string] -and $data[$language][$key] -is [string]){
                $tables[$language][$data['en'][$key]]=$data[$language][$key]
            }
        }
    }
}
# Native-only toolbar labels use stable canonical keys rather than accidental casing.
$native=@'
en|Select all|Select safe|Select advanced|De-select all|Restore safe|Restore advanced|Restore all defaults|Apply selected|Analyze / reload
id|Pilih semua|Pilih yang aman|Pilih tingkat lanjut|Batalkan semua pilihan|Pulihkan yang aman|Pulihkan tingkat lanjut|Pulihkan semua bawaan|Terapkan pilihan|Analisis / muat ulang
de|Alle auswählen|Sichere auswählen|Erweiterte auswählen|Auswahl aufheben|Sichere wiederherstellen|Erweiterte wiederherstellen|Alle Standardwerte wiederherstellen|Auswahl anwenden|Analysieren / neu laden
fr|Tout sélectionner|Sélectionner les options sûres|Sélectionner les options avancées|Tout désélectionner|Restaurer les options sûres|Restaurer les options avancées|Restaurer les valeurs par défaut|Appliquer la sélection|Analyser / recharger
ar|تحديد الكل|تحديد الخيارات الآمنة|تحديد الخيارات المتقدمة|إلغاء تحديد الكل|استعادة الخيارات الآمنة|استعادة الخيارات المتقدمة|استعادة الإعدادات الافتراضية|تطبيق المحدد|تحليل / إعادة تحميل
tl|Piliin lahat|Piliin ang ligtas|Piliin ang advanced|Alisin lahat ng pinili|Ibalik ang ligtas|Ibalik ang advanced|Ibalik lahat ng default|Ilapat ang pinili|Suriin / i-reload
vi|Chọn tất cả|Chọn mục an toàn|Chọn mục nâng cao|Bỏ chọn tất cả|Khôi phục mục an toàn|Khôi phục mục nâng cao|Khôi phục mặc định|Áp dụng mục đã chọn|Phân tích / tải lại
zh-CN|全选|选择安全选项|选择高级选项|取消全选|恢复安全选项|恢复高级选项|恢复所有默认值|应用所选项|分析 / 重新加载
zh-TW|全選|選取安全選項|選取進階選項|取消全選|還原安全選項|還原進階選項|還原所有預設值|套用選取項目|分析 / 重新載入
th|เลือกทั้งหมด|เลือกตัวเลือกที่ปลอดภัย|เลือกตัวเลือกขั้นสูง|ยกเลิกการเลือกทั้งหมด|คืนค่าตัวเลือกที่ปลอดภัย|คืนค่าตัวเลือกขั้นสูง|คืนค่าเริ่มต้นทั้งหมด|ใช้รายการที่เลือก|วิเคราะห์ / โหลดใหม่
ru|Выбрать всё|Выбрать безопасные|Выбрать расширенные|Снять выделение|Восстановить безопасные|Восстановить расширенные|Восстановить значения по умолчанию|Применить выбранное|Анализ / обновление
uk|Вибрати все|Вибрати безпечні|Вибрати розширені|Зняти виділення|Відновити безпечні|Відновити розширені|Відновити типові значення|Застосувати вибране|Аналіз / оновлення
pt|Selecionar tudo|Selecionar opções seguras|Selecionar opções avançadas|Desmarcar tudo|Restaurar opções seguras|Restaurar opções avançadas|Restaurar padrões|Aplicar seleção|Analisar / recarregar
ja|すべて選択|安全な項目を選択|詳細項目を選択|選択をすべて解除|安全な項目を復元|詳細項目を復元|既定値をすべて復元|選択した項目を適用|分析 / 再読み込み
ko|모두 선택|안전한 항목 선택|고급 항목 선택|모두 선택 해제|안전한 항목 복원|고급 항목 복원|기본값 모두 복원|선택 항목 적용|분석 / 새로 고침
ur|سب منتخب کریں|محفوظ اختیارات منتخب کریں|جدید اختیارات منتخب کریں|سب کا انتخاب ختم کریں|محفوظ اختیارات بحال کریں|جدید اختیارات بحال کریں|تمام طے شدہ اقدار بحال کریں|منتخب کردہ لاگو کریں|تجزیہ / دوبارہ لوڈ
ta|அனைத்தையும் தேர்ந்தெடு|பாதுகாப்பானவற்றைத் தேர்ந்தெடு|மேம்பட்டவற்றைத் தேர்ந்தெடு|அனைத்துத் தேர்வையும் நீக்கு|பாதுகாப்பானவற்றை மீட்டமை|மேம்பட்டவற்றை மீட்டமை|இயல்புநிலைகளை மீட்டமை|தேர்ந்தெடுத்தவற்றைப் பயன்படுத்து|ஆய்வு / மீளேற்று
hi|सभी चुनें|सुरक्षित विकल्प चुनें|उन्नत विकल्प चुनें|सभी का चयन हटाएँ|सुरक्षित विकल्प बहाल करें|उन्नत विकल्प बहाल करें|सभी डिफ़ॉल्ट बहाल करें|चयन लागू करें|विश्लेषण / पुनः लोड
ms|Pilih semua|Pilih yang selamat|Pilih lanjutan|Nyahpilih semua|Pulihkan yang selamat|Pulihkan lanjutan|Pulihkan semua lalai|Gunakan pilihan|Analisis / muat semula
jv|Pilih kabeh|Pilih sing aman|Pilih tingkat lanjut|Batalake kabeh pilihan|Pulihake sing aman|Pulihake tingkat lanjut|Pulihake kabeh gawan|Terapake pilihan|Analisis / muat maneh
ban|Pilih sami|Pilih sane aman|Pilih tingkat lanjut|Batalang sami pilihan|Pulihang sane aman|Pulihang tingkat lanjut|Pulihang sami bawaan|Terapang pilihan|Analisis / muat malih
sv|Välj alla|Välj säkra|Välj avancerade|Avmarkera alla|Återställ säkra|Återställ avancerade|Återställ alla standardvärden|Tillämpa valda|Analysera / läs in igen
es|Seleccionar todo|Seleccionar opciones seguras|Seleccionar opciones avanzadas|Deseleccionar todo|Restaurar opciones seguras|Restaurar opciones avanzadas|Restaurar valores predeterminados|Aplicar selección|Analizar / recargar
'@
$rows=$native.Trim() -split '\r?\n'
$keys=$rows[0].Split('|')
foreach($row in $rows){$cells=$row.Split('|');for($i=1;$i -lt $keys.Length;$i++){$tables[$cells[0]][$keys[$i]]=$cells[$i]}}
foreach($language in $tables.Keys){
    $table=$tables[$language]
    foreach($pair in @(@('Repair selected','REPAIR'),@('Enable selected','ENABLE'),@('Download & install','DOWNLOAD & INSTALL'),@('Official source','OPEN OFFICIAL DOWNLOAD'))){
        if($table.ContainsKey($pair[1])){$table[$pair[0]]=$table[$pair[1]]}
    }
}
$stream=[IO.MemoryStream]::new()
$gzip=[IO.Compression.GZipStream]::new($stream,[IO.Compression.CompressionLevel]::Optimal,$true)
$writer=[IO.BinaryWriter]::new($gzip,[Text.Encoding]::UTF8,$true)
$writer.Write([int]$tables.Count)
foreach($language in ($tables.Keys | Sort-Object)){
    $writer.Write([string]$language);$writer.Write([int]$tables[$language].Count)
    foreach($key in ($tables[$language].Keys | Sort-Object)){$writer.Write([string]$key);$writer.Write([string]$tables[$language][$key])}
}
$writer.Dispose();$gzip.Dispose()
[pscustomobject]@{Languages=$tables.Count;Entries=(@($tables.Values | ForEach-Object {$_.Count}) | Measure-Object -Sum).Sum;Payload=[Convert]::ToBase64String($stream.ToArray())} | ConvertTo-Json -Compress
$stream.Dispose()
