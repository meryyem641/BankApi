---
title: "Finansal Bütçe ve Harcama Yönetim Sistemi - 20 Günlük Staj Çalışma Raporu"
author: "Meryem"
date: "20 Ağustos 2026"
lang: tr-TR
toc: true
toc-title: "İçindekiler"
geometry: margin=2.5cm
fontsize: 11pt
colorlinks: true
linkcolor: purple
urlcolor: purple
---

# Proje Tanımı

Staj sürecinde ASP.NET Core (.NET 10), Entity Framework Core ve SQLite kullanılarak bir web uygulaması geliştirilmiştir. Proje, basit bir banka hesabı simülasyonu olarak başlamış; süreç ilerledikçe gelir/gider takibi, banka ekstresi içe aktarma, kart yönetimi, tasarruf hedefleri, tekrarlayan ödeme takibi ve otomatik bildirimler içeren kapsamlı bir **Finansal Bütçe ve Harcama Yönetim Sistemi**'ne dönüştürülmüştür.

Bu rapor, yirmi iş günü boyunca yapılan çalışmaları günlük olarak açıklamaktadır.

# 1. Gün - Proje Analizi ve Temel Sistem Mimarisi

## Yapılan çalışmalar

- Projenin amacı ve kapsamı belirlendi: banka çalışanının müşteri/kullanıcı yönetimi yaptığı, müşterinin ise kendi hesabını takip ettiği bir sistem.
- Gerekli veri modelleri planlandı: Kullanıcı, Müşteri, Hesap, Transfer, Para Yatırma Talebi, İşlem Geçmişi.
- Backend için ASP.NET Core Web API, veri katmanı için Entity Framework Core ve SQLite, frontend için tek sayfalık HTML/CSS/JavaScript mimarisine karar verildi.
- Proje iskeleti oluşturuldu, `Controllers`, `Models`, `Data`, `Services` ve `wwwroot` klasör yapısı kuruldu.

## Gün sonunda elde edilen sonuç

Projenin kapsamı, veri modeli ve teknoloji seçimleri netleşti; sonraki günlerin planı hazırlandı.

# 2. Gün - Kullanıcı Kaydı, Giriş ve JWT Kimlik Doğrulama

## Yapılan çalışmalar

- Kayıt formuna kullanıcı adı, e-posta, cep telefonu ve şifre alanları eklendi.
- Şifrelerin düz metin tutulmaması için PBKDF2-SHA256 (rastgele salt + 100.000 iterasyon) ile özetlenip saklanması sağlandı.
- Giriş başarılı olduğunda kullanıcı kimliğini ve rolünü taşıyan bir JWT üretildi; token `Authorization: Bearer` başlığıyla sonraki isteklere eklendi.
- Giriş ekranına "Kullanıcı Girişi" ve "Yönetici Girişi" seçenekleri ayrı kartlar olarak eklendi; backend tarafında da giriş türü kontrolü yapılarak bir rolün diğerinin girişini kullanamaması sağlandı.

## Gün sonunda elde edilen sonuç

Kayıt ve giriş akışı, JWT tabanlı kimlik doğrulamayla birlikte çalışır hale geldi.

# 3. Gün - Rol Bazlı Yetkilendirme ve Yönetici Onay Süreci

## Yapılan çalışmalar

- Yeni kayıt olan kullanıcılar doğrudan aktif hale getirilmedi; `IsApproved = false` durumuyla kaydedildi.
- Onaysız kullanıcı giriş denediğinde "onay bekleniyor" mesajı gösterildi.
- Yönetici panelinde "Onay Bekleyen Kullanıcılar" ekranı hazırlandı; onaylama işleminde kullanıcı için otomatik müşteri kaydı ve banka hesabı oluşturuldu.
- `[Authorize(Roles = "Admin")]` ile korunan uç noktalar belirlendi; frontend tarafında da menülerin role göre gizlenmesi sağlandı (asıl güvenlik her zaman backend'de).

## Gün sonunda elde edilen sonuç

Normal kullanıcı ile yönetici arasındaki yetki ayrımı hem arayüzde hem API katmanında sağlandı.

# 4. Gün - Yönetici Paneli ve Müşteri Yönetimi

## Yapılan çalışmalar

- Yönetici paneline müşteri ekleme, listeleme, güncelleme ve silme işlemleri eklendi.
- E-posta adresinin benzersiz olması zorunlu tutuldu; aynı e-posta ile ikinci kayıt engellendi. Telefon numarasının tekrarına ise izin verildi (kurumsal/ortak numara senaryoları için).
- Müşteri silme işleminde ilişkili hesap ve transfer kayıtlarının kontrollü biçimde temizlenmesi sağlandı; işlem audit kaydına yazıldı.

## Gün sonunda elde edilen sonuç

Yönetici panelinin temel müşteri yönetimi bölümü, veri doğrulama kurallarıyla birlikte tamamlandı.

# 5. Gün - Excel'den Toplu Müşteri Aktarımı, Arama ve Sayfalama

## Yapılan çalışmalar

- `.xlsx` dosyasının ZIP içindeki XML yapısı doğrudan okunarak (harici kütüphane kullanmadan) toplu müşteri aktarımı geliştirildi.
- Sabit sütun düzeni (A: Ad Soyad, B: E-posta, C: Telefon) ve kabul edilen başlık alternatifleri tanımlandı.
- Aktarım sırasında boş alan, geçersiz e-posta ve tekrarlanan kayıt kontrolleri eklendi; sonuçta kaç kaydın eklendiği/atlandığı kullanıcıya raporlandı.
- Müşteri listesine ad/e-posta/telefon araması ve 20 kayıt/sayfa şeklinde sayfalama eklendi.

## Gün sonunda elde edilen sonuç

Sistem, tek tek veri girişi yerine toplu veriyle çalışabilir hale geldi.

# 6. Gün - Hesap Yönetimi, Para Yatırma Talebi ve Onay Akışı

## Yapılan çalışmalar

- Kullanıcı onaylandığında kendisi için otomatik olarak `0 TL` bakiyeli bir hesap açıldı; bakiyenin yönetici tarafından elle girilmesi engellendi.
- Para yatırma işlemi doğrudan bakiye artırma yerine talep oluşturma şeklinde tasarlandı: kullanıcı talep oluşturur → talep `Pending` durumunda beklerken → yönetici onaylarsa bakiye artar, reddederse değişmez.
- Bir kullanıcının aynı anda birden fazla bekleyen para yatırma talebi oluşturabilmesine izin verildi.

## Gün sonunda elde edilen sonuç

Bakiye artışı, kontrolsüz bir alan değil, onaylanmış bir finansal hareketin sonucu haline geldi.

# 7. Gün - Para Transferi, İşlem Geçmişi ve Swagger Entegrasyonu

## Yapılan çalışmalar

- Hesaplar arası sistem içi para transferi eklendi; gönderen/alıcı hesabın aktifliği, bakiye yeterliliği ve hesap sahipliği kontrol edildi. Bakiye güncellemesi ve transfer kaydı aynı veritabanı işlemi (transaction) içinde yapılarak tutarsızlık riski azaltıldı.
- Kim, ne zaman, hangi işlemi yaptı sorusuna cevap veren bir `AuditLog` tablosu ve yönetici ekranı eklendi.
- Swashbuckle ile Swagger UI aktifleştirildi; JWT ile korunan uç noktaların `Authorize` üzerinden Bearer token girilerek test edilmesi sağlandı.

## Gün sonunda elde edilen sonuç

Bankacılık simülasyonunun temel akışı (kayıt → onay → hesap → para yatırma → transfer → işlem geçmişi) uçtan uca çalışır hale geldi ve Swagger üzerinden test edilebildi.

# 8. Gün - Projenin Bütçe Yönetim Sistemine Dönüşü

## Yapılan çalışmalar

- Kullanıcı geri bildirimleri doğrultusunda projenin kapsamı genişletilmesine karar verildi: yalnızca banka hesabı simülasyonu değil, kullanıcının **gerçek kişisel bütçesini** takip edebileceği bir sisteme dönüştürülmesi planlandı.
- `BudgetEntry` (gelir/gider kaydı) veri modeli tasarlandı: tür (gelir/gider), kategori, açıklama, tutar, tarih ve kayıt kaynağı alanlarını içerecek şekilde.
- Mevcut banka hesabı/transfer altyapısı kaldırılmadı; arka planda saklı tutulup arayüzde "Bütçem" odaklı yeni ekranlar öne çıkarıldı.

## Gün sonunda elde edilen sonuç

Projenin yönü, banka işlemi simülasyonundan kişisel bütçe yönetimine kaydırıldı; yeni veri modeli hazırlandı.

# 9. Gün - Gelir/Gider Kayıtları, Kategoriler ve Aylık Özet

## Yapılan çalışmalar

- Gelir/gider ekleme, güncelleme ve silme uç noktaları (`/api/v1/budget-entries`) yazıldı.
- Varsayılan bütçe kategorileri (Maaş, Kira, Market, Fatura, Ulaşım, Eğitim, Sağlık, Eğlence vb.) tanımlandı; yönetici için kategori yönetim ekranı eklendi.
- "Bütçem" ekranına toplam gelir, toplam gider, banka bakiyesi, eldeki nakit ve toplam varlık özetleyen kartlar eklendi.

## Gün sonunda elde edilen sonuç

Kullanıcı, manuel olarak gelir/gider girip anlık özet görebilir hale geldi.

# 10. Gün - Aylık Bütçe Limitleri ve Gider Dağılımı Grafiği

## Yapılan çalışmalar

- Kategori bazlı aylık bütçe limiti belirleme özelliği eklendi (`/api/v1/budget-limits`); harcanan tutar limitle karşılaştırılıp ilerleme çubuğuyla gösterildi, limit aşıldığında çubuk kırmızıya döndü.
- "Raporlarım" ekranına ay seçimi, kategori bazlı gider dağılımı ve Canvas ile çizilen bir pasta grafiği eklendi.
- Aylık gelir/gider/fark özetleri ve son kayıtlar listesi rapor ekranına taşındı.

## Gün sonunda elde edilen sonuç

Kullanıcı, harcamalarını kategori bazında görselleştirebilir ve bütçe limitlerini takip edebilir hale geldi.

# 11. Gün - Banka Ekstresi İçe Aktarma Motoru: Excel Ayrıştırma

## Yapılan çalışmalar

- Kullanıcının banka ekstresini elle tek tek girmek yerine dosyadan içe aktarabilmesi için `StatementImportController` geliştirilmeye başlandı.
- `.xlsx` ekstre dosyaları için ham OOXML/ZIP ayrıştırma ile satırların okunması sağlandı; Tarih, Açıklama, Tutar ve (varsa) Tür sütunlarının farklı başlık adı varyasyonları desteklendi.
- Türkçe (1.234,56) ve İngilizce (1,234.56) sayı biçimlerini otomatik ayırt eden bir tutar ayrıştırıcı yazıldı.
- İçe aktarılan satırlar önce bir önizleme ekranında gösterilip kullanıcı onayından sonra kaydedilecek şekilde tasarlandı.

## Gün sonunda elde edilen sonuç

Excel ekstrelerinin otomatik olarak gelir/gider kaydına dönüştürülmesi sağlandı.

# 12. Gün - Banka Ekstresi İçe Aktarma Motoru: PDF Ayrıştırma

## Yapılan çalışmalar

- PDF ekstreler için PdfPig kütüphanesi ile metin ve kelime koordinatlarının okunması sağlandı.
- Farklı bankaların sütunları farklı sırada verdiği görüldüğü için, başlık kelimelerinin (Tarih, Açıklama, Tutar, Bakiye vb.) x-koordinatından dinamik bir sütun düzeni çıkaran bir algoritma yazıldı.
- Koordinat tabanlı okuyucunun başlık bulamadığı (taranmış/düzensiz) PDF'ler için regex tabanlı bir yedek ayrıştırıcı eklendi; iki sonuçtan daha fazla işlem yakalayanı kullanıldı.
- Ekstre tarihlerinin veritabanında UTC, arayüzde ise `Europe/Istanbul` saatiyle gösterilmesi sağlandı.

## Gün sonunda elde edilen sonuç

Farklı bankalardan alınan hem PDF hem Excel ekstreler güvenilir biçimde okunabilir hale geldi.

# 13. Gün - Akıllı Kategori Tahmin Motoru ve Kişisel Kurallar

## Yapılan çalışmalar

- İçe aktarılan işlemlere otomatik kategori atayan bir motor yazıldı: market, yemek, ulaşım, fatura, giyim, eğlence gibi 20'den fazla kategoride onlarca anahtar kelime/işletme adı tanımlandı.
- Kullanıcının bir işlemi elle kategorize etmesi durumunda bu tercihin otomatik olarak kişisel bir kurala dönüşmesi sağlandı (`MerchantCategoryRule`); bir sonraki ekstrede aynı işletme adı otomatik doğru kategoriye düştü.
- Öncelik sırası netleştirildi: kullanıcı kuralı → genel anahtar kelime kümesi → eşleşme yoksa "Belirsiz" olarak işaretleyip kullanıcıdan onay isteme.
- Transfer, nakit çekme/yatırma gibi tutar hareketleri için ayrı bir "işlem amacı" (Borç verdim/aldım, Nakit çektim/yatırdım, Yatırım vb.) seçim ekranı eklendi.

## Gün sonunda elde edilen sonuç

Ekstre içe aktarımı sonrası manuel kategorileme ihtiyacı büyük ölçüde azaldı; sistem kullanıcı alışkanlıklarını öğrenir hale geldi.

# 14. Gün - Banka Hesabı / Kredi Kartı Ayrımı ve Çoklu Kart Desteği

## Yapılan çalışmalar

- `Account` modeline `Type` (Bank / CreditCard) ve kullanıcının verdiği `Name` alanları eklendi; tek bir tablo iki farklı finansal aracı temsil edecek şekilde genişletildi.
- Hesap oluşturma işlemi yönetici yetkisinden çıkarılıp kullanıcının kendi kartını/hesabını ekleyip adlandırabilmesi sağlandı; kart adı istenildiği zaman değiştirilebilir hale getirildi.
- Arayüze "Kartlarım" bölümü eklendi: banka hesabı ve kredi kartı ayrı ikon ve renkle gösterildi.

## Gün sonunda elde edilen sonuç

Kullanıcı artık birden fazla banka hesabı ve kredi kartını kendi başına yönetebilir hale geldi.

# 15. Gün - Kart Bazlı Ekstre Eşleştirme ve Cari Hesap Takibi

## Yapılan çalışmalar

- Ekstre içe aktarma uç noktaları, hangi karta ait olduğunu belirten bir `accountId` parametresi almaya başladı; kullanıcı ekstre yüklerken önce kartı seçecek şekilde arayüz güncellendi.
- Yinelenen kayıt kontrolü kullanıcı genelinden **kart bazına** indirildi; böylece aynı tutar/tarihli bir işlem iki farklı kartta ayrı ayrı kaydedilebildi, ama aynı karta iki kez yüklenmesi engellendi.
- Kart sahipliği doğrulaması eklendi: bir kullanıcı yalnızca kendi kartına ekstre yükleyebilir.
- Borç/alacak (cari hesap) kayıtlarının ayrı bir tabloda tutulması ve "Bana ödenecekler" / "Borçlarım" olarak listelenmesi tamamlandı.

## Gün sonunda elde edilen sonuç

Birden fazla kartın ekstresi karışmadan, birbirini etkilemeden yönetilebilir hale geldi.

# 16. Gün - Tasarruf Hedefleri ve Tekrarlayan Ödeme Takibi

## Yapılan çalışmalar

- `SavingsGoal` modeli ve `/api/v1/savings-goals` uç noktaları eklendi: hedef adı, hedef tutar, biriktirilen tutar ve hedef tarih. Kullanıcı hedefe istediği an para ekleyebiliyor, ilerlemesini çubukla görebiliyor.
- `RecurringPayment` modeli ve `/api/v1/recurring-payments` uç noktaları eklendi: ödeme adı, tutar, ayın günü ve kategori. Sistem, girilen güne göre bir sonraki ödeme tarihini otomatik hesaplıyor.
- Arayüze "Hedeflerim" ve "Abonelikler" bölümleri eklendi.

## Gün sonunda elde edilen sonuç

Kullanıcı, tek seferlik kayıtların ötesinde uzun vadeli hedeflerini ve düzenli ödemelerini de sistemde takip edebilir hale geldi.

# 17. Gün - Proaktif Bildirimler ve CSV Dışa Aktarma

## Yapılan çalışmalar

- Giriş anında çalışan bir kontrol eklendi: ödeme günü 3 gün veya daha az kalan aktif tekrarlayan ödemeler için hatırlatma bildirimi gösteriliyor.
- Aynı şekilde, bütçe limiti aşılmış veya %90'a yaklaşmış kategoriler için giriş anında uyarı bildirimi tetiklendi; kullanıcıyı yormamak için günde yalnızca bir kez gösterilecek şekilde sınırlandırıldı.
- Aylık bütçe raporunun tek tıkla `.csv` olarak indirilebilmesi sağlandı (tarayıcıda `Blob` ile dosya oluşturma).

## Gün sonunda elde edilen sonuç

Sistem, kullanıcıyı beklemek yerine önemli finansal durumları kendiliğinden bildirir hale geldi; rapor verisi dışa aktarılabilir oldu.

# 18. Gün - Kullanıcı Deneyimi: Özel Modal, Karanlık Mod ve PWA

## Yapılan çalışmalar

- Tarayıcının native `confirm`/`prompt` pencereleri (9 kullanım yeri) kaldırılıp uygulama tasarımına uygun özel bir onay/isim girme modalı ile değiştirildi (Enter ile onay, Esc ile vazgeçme).
- CSS özel değişken (custom properties) tabanlı bir karanlık mod sistemi eklendi; tercih `localStorage`'da saklanıp bir sonraki ziyarette hatırlanıyor.
- `manifest.json` ve bir service worker (`sw.js`) eklenerek uygulamaya PWA (ana ekrana eklenebilir, statik kabuğu önbellekleyen) desteği kazandırıldı.

## Gün sonunda elde edilen sonuç

Uygulama, tarayıcı varsayılan bileşenlerine bağımlı olmaktan çıkıp kendi tutarlı arayüz dilini kazandı.

# 19. Gün - Profesyonel Arayüz Yenilemesi ve API Dokümantasyonu

## Yapılan çalışmalar

- Sistem fontu Arial'den modern bir arayüz yazı tipi ailesine değiştirildi; başlık hiyerarşisi ve buton/köşe yuvarlaklığı tutarlı hale getirildi.
- Sidebar'daki Unicode karakter ikonları, 16 adet elle tasarlanmış tutarlı SVG ikonla değiştirildi; marka rengine uygun bir favicon eklendi.
- Tekrar eden/gereksiz arayüz öğeleri (üst menüdeki ikinci "Profilim" butonu, kullanılmayan "Kontrol Paneli" başlığı) kaldırıldı.
- Swagger etiketleri gözden geçirildi; yeni eklenen Tasarruf Hedefleri ve Tekrarlayan Ödemeler uç noktalarına Türkçe etiket eklendi. Ekip için gerçek istekler içeren bir Bruno koleksiyonu (`bruno/` klasörü) hazırlandı.

## Gün sonunda elde edilen sonuç

Uygulama görsel olarak daha profesyonel bir izlenim verir hale geldi; API dokümantasyonu ve test araçları tamamlandı.

# 20. Gün - Genel Test, Hata Düzeltme ve Sunum Hazırlığı

## Yapılan çalışmalar

- Uçtan uca test senaryoları tekrar çalıştırıldı: kayıt/onay akışı, kart oluşturma, ekstre içe aktarma, bütçe limiti uyarısı, tasarruf hedefi/tekrarlayan ödeme akışları.
- Karşılaşılan teknik sorunlar ve çözümleri gözden geçirildi: farklı bankaların PDF sütun sıralaması, kart bazlı yinelenen kayıt kontrolü, canlı veritabanı şemasının kayıp vermeden (idempotent `ALTER TABLE`) güncellenmesi.
- Projenin mimarisini, teknik kararlarını ve gelişim sürecini anlatan bir sunum hazırlandı; sunuma gerçek ekran görüntüleri, uç nokta listesi ve canlı istek/cevap örnekleri eklendi.

## Gün sonunda elde edilen sonuç

Proje, staj sürecinin başındaki basit banka simülasyonundan; ekstre okuma, akıllı kategorileme, çoklu kart desteği, otomasyon ve bildirimler içeren eksiksiz bir kişisel finans platformuna dönüşmüş oldu.

# Genel Değerlendirme

Yirmi günlük çalışma sonunda proje, kullanıcı kaydı ve rol bazlı yetkilendirmeden başlayıp; müşteri yönetimi, toplu veri aktarımı, banka ekstresi okuma motoru, akıllı kategorileme, çoklu kart desteği, tasarruf hedefleri, tekrarlayan ödeme takibi, proaktif bildirimler, karanlık mod, PWA desteği ve profesyonel bir arayüze kadar uzanan kapsamlı bir web uygulamasına dönüştürülmüştür.

Sürecin en önemli sonucu, projenin sabit bir kapsamla değil; kullanıcı ihtiyacına göre **evrilerek** geliştirilmiş olmasıdır — basit bir banka simülasyonundan gerçek bir kişisel bütçe yönetim platformuna geçiş, hem teknik hem ürün kararı alma becerisini geliştirmiştir. JWT tabanlı güvenlik, veri doğrulama, rol bazlı erişim, transaction kullanımı ve idempotent veritabanı geçişleri gibi yaklaşımlar boyunca korunmuş; her yeni özellik mevcut mimariyi bozmadan eklenmiştir.

Swagger ve Bruno ile API katmanı bağımsız test edilebilir hale gelmiş, hazırlanan sunum ile projenin teknik kararları ve gelişim süreci somut ekran görüntüleri ve canlı örneklerle görselleştirilmiştir.
