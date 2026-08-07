---
title: "Banka Yönetim Sistemi"
subtitle: "Proje Geliştirme ve Teknik Uygulama Raporu"
author: "Meryem"
date: "3 Ağustos 2026"
lang: tr-TR
toc: true
toc-title: "İçindekiler"
geometry: margin=2.5cm
fontsize: 11pt
colorlinks: true
linkcolor: purple
urlcolor: purple
---

# 1. Yönetici Özeti

Bu proje, banka çalışanlarının müşteri ve kullanıcı yönetimi yapabildiği; müşterilerin ise kendi hesaplarını ve banka içi işlemlerini güvenli biçimde takip edebildiği bir **Banka Yönetim Sistemi** olarak geliştirilmiştir.

Projenin temel amacı, yalnızca basit bir müşteri ekleme ekranı oluşturmak değil; gerçek bankacılık uygulamalarında bulunan kullanıcı rolleri, onay süreçleri, hesap sahipliği, bakiye hareketleri, işlem geçmişi, veri aktarımı ve API güvenliği gibi kavramları çalışan bir prototip üzerinde göstermektir.

Sistem iki ana kullanıcı tipine ayrılmıştır:

- **Admin:** Kullanıcıları ve müşterileri yönetir, kayıtları onaylar veya reddeder, hesap yönetimi yapar, Excel’den toplu müşteri aktarır ve sistem işlem geçmişini izler.
- **Normal kullanıcı:** Yalnızca kendi profilini, kendisine bağlı hesapları ve kendi bankacılık işlemlerini görür; para yatırma talebi oluşturabilir ve sistem içi transfer yapabilir.

Projenin önemli bir tasarım kararı, gerçek bir bankaya veya gerçek para transferi altyapısına bağlanmak yerine **kontrollü bir banka simülasyonu** olarak çalışmasıdır. Para yatırma talepleri admin onayına tabi tutulur; onay sonrasında bakiye artar ve kullanıcı bu bakiye üzerinden sistem içi transfer gerçekleştirebilir.

# 2. Projenin Amacı ve Kapsamı

## 2.1. Çözülmek istenen problem

Geleneksel bir yönetim ekranında yalnızca kayıt eklemek yeterli değildir. Bir banka sistemi için aşağıdaki soruların cevaplanması gerekir:

1. Sisteme kim giriş yapabilir?
2. Normal kullanıcı hangi verilere erişebilir?
3. Kullanıcının sisteme kaydı ne zaman aktifleşir?
4. Bir müşteri kaydı nasıl oluşturulur ve doğrulanır?
5. Bir hesap nasıl açılır?
6. Bakiye hangi işlem sonucunda değişir?
7. Admin’in yaptığı işlemler nasıl takip edilir?
8. Çok sayıda müşteri sisteme tek tek yazılmadan nasıl aktarılır?
9. API endpoint’leri nasıl güvenli şekilde test edilir?

Bu proje, bu sorulara hem kullanıcı arayüzü hem de backend API katmanında cevap verecek şekilde geliştirilmiştir.

## 2.2. Kapsam dahilindeki özellikler

- Admin ve normal kullanıcı girişi
- JWT tabanlı kimlik doğrulama
- Rol bazlı yetkilendirme
- Kullanıcı kayıt ve admin onay süreci
- Müşteri oluşturma, güncelleme ve silme
- Excel’den toplu müşteri aktarımı
- E-posta benzersizlik kontrolü
- Profil bilgisi ve şifre güncelleme
- Hesap oluşturma ve hesap sahipliği
- Para yatırma talebi ve admin onayı
- Sistem içi para transferi simülasyonu
- İşlem ve denetim geçmişi
- Sayfalama ve müşteri arama
- Swagger arayüzü ve JWT ile API testi
- Responsive, mor temalı kullanıcı arayüzü

## 2.3. Kapsam dışında kalanlar

- Gerçek banka hesabına bağlanma
- Gerçek EFT/Havale veya ödeme kuruluşu entegrasyonu
- Gerçek para hareketi
- SMS veya e-posta servisinden canlı doğrulama gönderimi
- Üretim ortamı için fiziksel güvenlik ve mevzuat süreçleri

Bu sınırlamalar bilinçli olarak belirlenmiştir. Projenin hedefi gerçek para taşımak değil, banka sisteminin iş akışlarını güvenli bir prototip üzerinde göstermektir.

# 3. Kullanılan Teknolojiler

| Katman | Kullanılan teknoloji | Kullanım amacı |
|---|---|---|
| Backend | ASP.NET Core Web API | REST endpoint’lerini oluşturmak |
| Dil | C# | Backend iş mantığı ve veri işlemleri |
| Hedef framework | .NET 10 | Uygulama çalışma altyapısı |
| ORM | Entity Framework Core | Veritabanı işlemlerini nesne tabanlı yürütmek |
| Veritabanı | SQLite | Yerel ve hızlı prototip veritabanı |
| Kimlik doğrulama | JWT Bearer | Token tabanlı oturum yönetimi |
| Şifreleme | PBKDF2 + SHA-256 | Şifreleri düz metin saklamamak |
| Frontend | HTML, CSS, JavaScript | Tek sayfalı kullanıcı arayüzü |
| API dokümantasyonu | Swagger / Swashbuckle | Endpoint görüntüleme ve test |
| Veri aktarımı | XLSX XML/ZIP okuma | Excel dosyasından müşteri aktarımı |

Frontend ayrı bir framework yerine statik HTML, CSS ve JavaScript ile hazırlanmıştır. Böylece proje daha kolay çalıştırılabilir ve backend API ile frontend arasındaki veri akışı doğrudan görülebilir hale gelmiştir.

# 4. Sistem Mimarisi

Sistem üç ana parçadan oluşur:

1. **Kullanıcı arayüzü:** Kullanıcının giriş yaptığı, admin veya normal kullanıcı işlemlerini gerçekleştirdiği web ekranı.
2. **API katmanı:** Kimlik doğrulama, müşteri, kullanıcı, hesap, transfer ve para yatırma işlemlerini yöneten controller’lar.
3. **SQLite veritabanı:** Kullanıcı, müşteri, hesap, transfer, para yatırma ve audit kayıtlarını saklayan veri katmanı.

## 4.1. Temel veri ilişkileri

```text
User  ──────── 0..1 Customer
Customer ───── 1..N Account
Account ────── 1..N Transfer (gönderen veya alıcı)
Account ────── 1..N DepositRequest
AuditLog ───── işlemi gerçekleştiren kullanıcıyı ve açıklamayı tutar
```

Bir normal kullanıcı bir müşteriye bağlanır. Müşterinin bir veya daha fazla hesabı olabilir. Hesaplar arası transferler `Transfer` tablosuna, para yatırma talepleri ise `DepositRequest` tablosuna kaydedilir.

## 4.2. Temel tablolar

### Users

Kullanıcı adı, e-posta, telefon, şifre özeti, rol, onay durumu ve müşteri bağlantısını içerir.

### Customers

Ad soyad, e-posta ve telefon bilgilerini tutar. Müşteriler yönetim açısından admin panelinde görüntülenir.

### Accounts

Müşteri ID’si, hesap numarası, bakiye, aktiflik durumu ve oluşturulma tarihini içerir.

### Transfers

Gönderen hesap, alıcı hesap, tutar, durum, oluşturulma tarihi ve tamamlanma tarihini kaydeder.

### DepositRequests

Kullanıcının para yatırma talebini, hesabını, tutarını, `Pending`, `Approved` veya `Rejected` durumunu ve inceleme tarihini tutar.

### AuditLogs

İşlemi yapan kullanıcı, işlem türü, açıklama ve tarih bilgilerini saklar. Böylece “kim, ne yaptı, ne zaman yaptı?” sorusu cevaplanabilir.

# 5. Geliştirme Süreci: Adım Adım Yapılanlar

## 5.1. İlk web uygulamasının oluşturulması

İlk aşamada banka yönetim sisteminin temel web ekranı oluşturuldu. Kullanıcıların ve adminin aynı uygulama üzerinden giriş yapabileceği bir yapı kuruldu.

İlk hedefler:

- Web uygulamasının çalışması
- Veritabanının oluşturulması
- Müşteri kayıtlarının tutulması
- Admin tarafından müşteri eklenebilmesi
- Kullanıcıların sisteme giriş yapabilmesi

Bu aşama sonraki bütün özelliklerin temelini oluşturdu.

## 5.2. Modern giriş ve kayıt ekranının hazırlanması

Giriş ekranı iki ayrı seçenek içerecek şekilde düzenlendi:

- Kullanıcı Girişi
- Yönetici Girişi

Bu ayrım yalnızca görsel bir tercih değildir. Login isteğine `LoginType` bilgisi eklenerek backend tarafında da kontrol edilir. Böylece admin hesabı kullanıcı girişinden, normal kullanıcı hesabı da admin girişinden kullanılamaz.

Kayıt ekranına şu alanlar eklendi:

- Kullanıcı adı
- E-posta
- Cep telefonu
- Şifre

Şifre göster/gizle özelliği, “Hesabın yok mu? Kayıt Ol” ve “Zaten hesabın var mı? Giriş Yap” geçişleri de bu aşamada eklendi.

## 5.3. JWT kimlik doğrulama

Kullanıcı başarılı giriş yaptığında backend tarafından JWT token üretilir. Token içinde kullanıcının ID’si, kullanıcı adı ve rolü bulunur.

Frontend sonraki isteklerde token’ı şu header ile gönderir:

```http
Authorization: Bearer <token>
```

Backend token’ın imzasını, issuer bilgisini, audience bilgisini ve süresini kontrol eder. Süresi dolmuş veya geçersiz token ile korumalı endpoint’lere erişilemez.

## 5.4. Şifre güvenliği

Şifreler veritabanına düz metin olarak yazılmaz. Şifre için salt üretilir ve PBKDF2 tabanlı SHA-256 özeti saklanır.

Bu yaklaşım sayesinde veritabanı ele geçirilse bile kullanıcı şifreleri doğrudan okunamaz. Ayrıca kullanıcı profilinden mevcut şifresini doğrulayarak yeni şifre belirleyebilir.

## 5.5. Kullanıcı onay süreci

Kayıt olan kullanıcı doğrudan aktif kullanıcı yapılmadı. Yeni kullanıcı ilk aşamada onaysızdır.

Akış:

1. Kullanıcı kayıt formunu doldurur.
2. Kullanıcı `IsApproved = false` olarak kaydedilir.
3. Kullanıcı giriş yapmayı denerse “onay bekleniyor” mesajı görür.
4. Admin, “Onay Bekleyen Kullanıcılar” ekranından başvuruyu inceler.
5. Admin onaylarsa kullanıcı aktifleşir.
6. Normal kullanıcı için müşteri bağlantısı ve banka hesabı oluşturulur.
7. Admin reddederse kullanıcı başvurusu silinir ve bakiye/hesap oluşmaz.

Bu yapı, banka hesabı açılış sürecinin basitleştirilmiş bir simülasyonudur.

## 5.6. Profil ekranı

Profil ekranı yalnızca üst menüdeki “Profilim” butonuna basıldığında açılacak şekilde düzenlendi.

Profilde gösterilen bilgiler:

- Kullanıcı adı
- E-posta
- Cep telefonu
- Rol

Kullanıcı kendi e-posta ve telefonunu güncelleyebilir. Güncelleme işleminde e-posta formatı ve telefon uzunluğu kontrol edilir.

## 5.7. Admin ve normal kullanıcı ayrımı

İlk aşamalarda normal kullanıcıların müşteri listesini görmemesi gerektiği fark edildi. Bu nedenle hem frontend hem backend tarafında rol kontrolü uygulandı.

Normal kullanıcı:

- Müşteri listesini göremez.
- “Müşterileri Getir” butonunu göremez.
- Müşteri araması yapamaz.
- Başka kullanıcıların hesaplarını göremez.
- Yalnızca kendi hesabına ait transferleri görebilir.

Admin:

- Müşterileri yönetebilir.
- Kullanıcıları onaylayabilir, reddedebilir veya silebilir.
- Hesap yönetebilir.
- Para yatırma taleplerini inceleyebilir.
- İşlem geçmişini görüntüleyebilir.

Frontend’de menülerin gizlenmesi kullanıcı deneyimini düzeltir; ancak asıl güvenlik backend’deki `[Authorize(Roles = "Admin")]` kontrolleriyle sağlanır.

# 6. Müşteri Yönetimi

## 6.1. Manuel müşteri ekleme

Admin panelinde ad soyad, e-posta ve telefon girilerek müşteri eklenebilir. Backend tarafında aşağıdaki kontroller yapılır:

- Ad soyad boş olamaz.
- E-posta geçerli formatta olmalıdır.
- Telefon en az 10 haneli olmalıdır.
- Aynı e-posta ikinci kez kullanılamaz.
- Telefon numarası aynı olabilir.

Telefonun benzersiz olmaması bilinçli bir karardır; aynı telefon numarası bazı sistemlerde birden fazla kişi veya kayıt için kullanılabilir. E-posta ise kullanıcıyı ayırt etmek için benzersiz tutulmuştur.

## 6.2. Güncelleme ve silme

Admin müşterinin bilgilerini güncelleyebilir. Silme işleminde müşteriyle bağlantılı hesap ve transfer kayıtları da kontrollü biçimde ele alınır.

Silme işleminde:

1. Müşterinin hesapları bulunur.
2. Bu hesaplarla ilişkili transferler bulunur.
3. Transfer kayıtları ve hesaplar temizlenir.
4. Müşteri kaydı silinir.
5. İşlem audit geçmişine yazılır.

Bu yaklaşım ilişkili kayıtlar nedeniyle oluşabilecek veritabanı hatalarını azaltır.

## 6.3. Müşteri arama ve sayfalama

Müşteri listesine arama özelliği eklendi. Arama şu alanlarda yapılır:

- Ad soyad
- E-posta
- Telefon

Liste performansını korumak için 20 kayıt bir sayfada gösterilecek şekilde sayfalama uygulanmıştır. `1. sayfa`, `2. sayfa` mantığıyla kayıtlar arasında geçiş yapılabilir.

# 7. Excel’den Toplu Müşteri Aktarımı

Müşterileri tek tek yazmak yerine Excel dosyasından aktarabilmek için admin paneline Excel aktarım alanı eklendi.

## 7.1. Dosya formatı

Sistem `.xlsx` dosyası kabul eder. `.numbers` dosyası doğrudan okunmaz; Numbers dosyası önce Excel `.xlsx` formatına aktarılmalıdır.

Beklenen üç sütun düzeni:

| Sütun | Anlam |
|---|---|
| A | Ad Soyad / Müşteri İsmi |
| B | E-posta |
| C | Telefon / Telefon Numarası / Telefon No / Cep Telefonu / No / Tel |

Sütunların konumu sabittir: isim A’da, e-posta B’de, telefon C’de olmalıdır. Başlık adlarında kabul edilen alternatifler desteklenir.

## 7.2. Teknik çalışma mantığı

XLSX dosyası aslında ZIP içinde XML dosyalarından oluşur. İçe aktarma kodu:

1. Dosyanın `.xlsx` olup olmadığını kontrol eder.
2. Excel arşivini açar.
3. İlk çalışma sayfasındaki satırları okur.
4. Paylaşılan metinleri çözer.
5. Başlık satırını ve sütun konumlarını kontrol eder.
6. Her satırın ad, e-posta ve telefon bilgisini alır.
7. E-postayı normalize eder.
8. Telefonu yalnızca rakamlardan oluşan biçime getirir.
9. Aynı e-posta varsa kaydı atlar.
10. Geçersiz satırları atlanan kayıt sayısına ekler.
11. Geçerli kayıtları SQLite veritabanına ekler.

## 7.3. Veri doğrulama

Örnek olarak `nisaozdemir01@example.com` daha önce kayıtlıysa aynı e-posta ile yeni müşteri eklenemez. Telefon numarası tekrar etse bile kayıt kabul edilir.

İçe aktarma sonucunda kullanıcıya:

- Kaç kayıt aktarıldığı
- Kaç kayıt atlandığı
- Hangi e-postaların daha önce mevcut olduğu

bilgisi verilir.

# 8. Kullanıcı Ana Sayfası ve Arayüz Geliştirmeleri

## 8.1. Kullanıcı dashboard’u

Normal kullanıcı giriş yaptıktan sonra banka uygulaması hissi veren bir ana sayfa görür.

Dashboard’da:

- Hoş geldin mesajı
- Toplam bakiye
- Aktif hesap sayısı
- Son işlem sayısı
- Son işlemler listesi
- Hesaplarımı Gör butonu
- Para Transferi butonu
- Profilimi Aç butonu

yer alır.

Bu bilgiler statik değildir; API’den gelen gerçek verilerle doldurulur.

## 8.2. Hesap kartları

Hesaplarım bölümü klasik tablo görünümünden çıkarılıp banka kartı görünümüne dönüştürüldü. Her kartta:

- Hesap türü
- Bakiye
- Hesap numarası
- Aktiflik durumu
- Transferde kullan seçeneği

gösterilir.

## 8.3. Mor temalı görsel kimlik

Yeni arka plan görseliyle uyum sağlamak için mavi vurgu renkleri mor tona çevrildi. Butonlar, sidebar aktif alanı, hesap kartları, giriş ikonları ve hover efektleri aynı mor renk ailesini kullanır.

Giriş sonrası karşılama mesajı da ekranı kaplayan bir modal yerine sağ tarafta küçük bir bildirim kartı olarak tasarlandı. Mesaj dinamik olarak `HOŞ GELDİN, KULLANICI!` formatında gösterilir. Mesaj 20 saniye sonra kapanır veya çarpı butonuyla erken kapatılabilir.

## 8.4. Tek sistem mesajı alanı

Klasik tarayıcı `alert` pencerelerinin kullanıcı deneyimini bozduğu görüldü. Bunun yerine uygulama içinde tek bir sistem mesajı alanı oluşturuldu.

Başarılı ve hatalı işlemler aynı alanda gösterilir. Mesaj birkaç saniye sonra kapanır ve kullanıcı başka bir sayfaya gönderilmez.

# 9. Hesap Yönetimi ve Para Yatırma Mantığı

## 9.1. Hesap açılışı

Kullanıcı admin tarafından onaylandıktan sonra normal kullanıcı için otomatik banka hesabı oluşturulur. Hesap başlangıçta `0 TL` bakiyeyle açılır.

Bu yaklaşımda admin’in müşteriye kafasına göre bakiye yazması engellenmiştir. Çünkü gerçek bankacılık mantığında bakiye, bir finansal hareketin sonucu olmalıdır.

## 9.2. Para yatırma talebi

Kullanıcı hesabına para yatırmak istediğinde doğrudan bakiyeyi artıramaz. Bunun yerine talep oluşturur.

Akış:

1. Kullanıcı hesabını seçer.
2. Tutar girer.
3. Para yatırma talebi oluşturur.
4. Talep `Pending` durumuna kaydedilir.
5. Admin talebi görür.
6. Admin onaylarsa hesap bakiyesi artar.
7. Talep `Approved` durumuna geçer.
8. Admin reddederse bakiye değişmez ve talep `Rejected` olur.

Aynı hesap için birden fazla para yatırma talebi oluşturulabilir. Örneğin kullanıcı 1.000 TL ve daha sonra 2.000 TL olmak üzere iki ayrı talep oluşturabilir. Admin bu talepleri tek tek sonuçlandırır.

## 9.3. Son işlemlerde gösterim

Son işlemler başlangıçta yalnızca transfer tablosunu gösteriyordu. Para yatırma işlemlerinin ayrı tabloda tutulduğu fark edilince dashboard; transfer ve para yatırma kayıtlarını birleştirecek şekilde güncellendi.

Kullanıcı artık son işlemlerde:

- Para transferi
- Para yatırma
- Tutar
- Hesap numarası
- Bekliyor, onaylandı veya reddedildi durumu

bilgilerini birlikte görür.

# 10. Sistem İçi Para Transferi

Bu proje gerçek bir banka API’sine bağlı değildir. Transfer özelliği, banka işlemlerini göstermek için hazırlanmış bir **sistem içi para transferi simülasyonudur**.

Örnek akış:

```text
Meryem hesabı: 10.000 TL
Demo hesabı:    1.000 TL

Meryem 500 TL gönderir.

Meryem hesabı: 9.500 TL
Demo hesabı:    1.500 TL
```

Transfer sırasında:

1. Gönderen ve alıcı hesabın farklı olup olmadığı kontrol edilir.
2. Hesapların aktifliği kontrol edilir.
3. Tutarın pozitif olması kontrol edilir.
4. Gönderen hesabın bakiyesi yeterli mi kontrol edilir.
5. Normal kullanıcı için gönderen hesabın gerçekten kendi hesabı olduğu doğrulanır.
6. Gönderen bakiyesi azaltılır.
7. Alıcı bakiyesi artırılır.
8. Transfer `Completed` durumuyla kaydedilir.

Bakiye değişimi ve transfer kaydı aynı veritabanı transaction’ı içinde yapılır. Böylece bir işlem yarıda kalırsa yalnızca bakiyenin değişip transfer kaydının oluşmaması gibi tutarsızlıkların önüne geçilir.

# 11. İşlem Geçmişi ve Denetim Kaydı

Sisteme admin panelinde ayrı bir **İşlem Geçmişi** bölümü eklendi.

Kaydedilen örnek işlemler:

- Müşteri eklendi
- Müşteri güncellendi
- Müşteri silindi
- Excel’den müşteri aktarıldı
- Kullanıcı onaylandı
- Kullanıcı reddedildi
- Kullanıcı silindi
- Para yatırma onaylandı
- Para yatırma reddedildi

Her kayıtta:

- İşlemi yapan kullanıcı
- İşlem türü
- Açıklama
- Türkiye saatine göre ekranda gösterilen tarih

bulunur.

Veritabanında zaman UTC olarak tutulur. Kullanıcı arayüzünde ise tarih ve saat `Europe/Istanbul` zaman dilimine çevrilerek gösterilir. Bu yaklaşım veri saklama açısından tutarlı, görüntüleme açısından kullanıcıya uygundur.

# 12. Swagger ve API Testi

Swagger UI eklenerek endpoint’ler görsel bir arayüz üzerinden test edilebilir hale getirildi.

Swagger adresi:

```text
http://localhost:5076/swagger
```

## 12.1. Kullanım adımları

1. `/api/v1/auth/login` endpoint’i açılır.
2. `Try it out` seçilir.
3. Kullanıcı adı, şifre ve `loginType` girilir.
4. `Execute` çalıştırılır.
5. Dönen token kopyalanır.
6. Swagger’daki `Authorize` butonuna basılır.
7. Token şu formatta girilir:

```text
Bearer eyJhbGciOi...
```

8. Bundan sonra JWT isteyen endpoint’ler test edilir.

## 12.2. Test edilebilecek örnek endpoint’ler

- `GET /api/v1/auth/me`
- `GET /api/v1/accounts`
- `GET /api/v1/transfers`
- `GET /api/v1/deposit-requests`
- `POST /api/v1/deposit-requests`
- `GET /api/v1/customers` — yalnızca Admin
- `GET /api/v1/users` — yalnızca Admin
- `GET /api/v1/audit-logs` — yalnızca Admin

Swagger ile yapılan çalışma, frontend olmadan backend API’lerinin ayrı ayrı test edilebildiğini göstermektedir.

# 13. Yetkilendirme Matrisi

| İşlem | Admin | Normal kullanıcı |
|---|---:|---:|
| Giriş | Evet | Evet |
| Profilini görüntüleme | Evet | Evet |
| Kendi profilini güncelleme | Evet | Evet |
| Müşteri listesini görüntüleme | Evet | Hayır |
| Müşteri ekleme/güncelleme/silme | Evet | Hayır |
| Kullanıcıları görüntüleme | Evet | Hayır |
| Kullanıcı onaylama/reddetme | Evet | Hayır |
| Kendi hesaplarını görüntüleme | Evet | Evet |
| Başka kullanıcıların hesabını görüntüleme | Evet | Hayır |
| Para yatırma talebi oluşturma | İsteğe bağlı | Evet |
| Para yatırma talebini onaylama | Evet | Hayır |
| Kendi transferlerini görüntüleme | Evet | Evet |
| Başka kullanıcıların transferlerini görüntüleme | Evet | Hayır |
| İşlem geçmişini görüntüleme | Evet | Kendi işlemleri |
| Swagger endpoint erişimi | Token’a göre | Token’a göre |

# 14. Doğrulama ve Hata Yönetimi

Projede doğrulama hem frontend hem backend katmanında yapılır. Frontend kullanıcıya hızlı geri bildirim verir; backend ise güvenliği sağlayan son kontrol noktasıdır.

Kontrol edilen örnekler:

- Boş zorunlu alanlar
- Geçersiz e-posta
- Yetersiz telefon uzunluğu
- Aynı e-posta ile tekrar kayıt
- Aktif olmayan hesap
- Yetersiz bakiye
- Kendine transfer
- Onaysız kullanıcı girişi
- Yetkisiz admin endpoint erişimi
- Geçersiz veya süresi dolmuş JWT
- Yanlış Excel formatı
- Eksik veya hatalı Excel başlıkları

Tarayıcı popup’ları yerine uygulama içi tek sistem mesajı alanı kullanılmıştır. Bu mesaj alanı başarı ve hata durumlarını ayırt eder.

# 15. Karşılaşılan Sorunlar ve Çözümler

## 15.1. Silme işleminde “İşlem başarısız” mesajı

Müşteri silinirken ilişkili hesap ve transfer kayıtları nedeniyle veritabanı ilişkileri sorun oluşturabiliyordu. Silme akışı transaction, ilişkili kayıtların kontrollü temizlenmesi ve ardından müşteri silinmesi şeklinde düzenlendi.

## 15.2. Profil bilgilerinin “Belirtilmemiş” görünmesi

Demo veya eski kullanıcı kayıtlarında e-posta ve telefon alanları boştu. Kullanıcı modeline bu alanlar eklendi, profil endpoint’i güncellendi ve kayıt sırasında e-posta/telefon zorunlu hale getirildi.

## 15.3. Yeni kullanıcının admin panelinde müşteri olarak görünmemesi

Onaysız kullanıcı için müşteri kaydı hemen oluşturulmuyordu. Tasarım kararı olarak müşteri kaydının admin onayından sonra oluşturulmasına geçildi.

## 15.4. Aynı e-posta ile müşteri eklenmesi

Eski kayıtlarda duplicate kontrolü yeterli değildi. E-posta normalize edildi, veritabanında benzersiz e-posta index’i oluşturuldu ve manuel ekleme/Excel aktarımı/güncelleme işlemlerinde duplicate kontrolü eklendi.

## 15.5. Telefon numarası tekrarları

Telefonun benzersiz olması gerektiği varsayımı değiştirildi. Son karara göre telefon aynı olabilir, e-posta aynı olamaz. Bu nedenle telefon unique index’leri kaldırıldı; e-posta unique bırakıldı.

## 15.6. Excel dosyalarının reddedilmesi

Dosyanın üç sütunlu olması tek başına yeterli değildi. Başlıkların ve sütun sırasının da kontrol edilmesi gerektiği görüldü. Sistem A/B/C konumlarını ve kabul edilen başlık alternatiflerini kontrol edecek şekilde düzenlendi.

## 15.7. Para yatırma talebinin listede görünmemesi

Para yatırma talepleri transferlerden ayrı tabloda tutulduğu için son işlemlerde görünmüyordu. Dashboard sorgusu transfer ve para yatırma kayıtlarını birleştirecek şekilde güncellendi.

## 15.8. Para yatırma hesabı listesinin boş olması

Onaylı bazı kullanıcıların müşteri bağlantısı olmasına rağmen hesabı yoktu. Onay akışında otomatik hesap oluşturma ve uygulama başlangıcında mevcut hesapsız kullanıcıları tamamlayan kontrol eklendi.

## 15.9. Swagger paket çakışması

Projede yerleşik OpenAPI paketi ile Swashbuckle sürümü arasında namespace ve paket sürümü çakışması oluştu. Swagger UI için tek bir Swashbuckle yaklaşımı kullanıldı ve çakışan yerleşik OpenAPI referansı kaldırıldı.

# 16. Test Senaryoları

## 16.1. Kullanıcı kayıt ve onay testi

1. Kayıt ekranından yeni kullanıcı oluşturulur.
2. Kullanıcı ile giriş denenir.
3. Onay bekleniyor mesajı kontrol edilir.
4. Admin ile giriş yapılır.
5. Kullanıcı onaylanır.
6. Kullanıcı tekrar giriş yapar.
7. Profil, müşteri bağlantısı ve hesap oluşumu kontrol edilir.

## 16.2. Yetki testi

1. Normal kullanıcı token’ı alınır.
2. `GET /api/v1/customers` denenir.
3. Erişimin engellendiği kontrol edilir.
4. Admin token’ı alınır.
5. Aynı endpoint tekrar denenir.
6. Müşteri listesinin geldiği kontrol edilir.

## 16.3. Para yatırma testi

1. Kullanıcı kendi hesabını seçer.
2. 1.000 TL talep oluşturur.
3. Dashboard’da talebin `Bekliyor` göründüğü kontrol edilir.
4. Aynı hesap için ikinci talep oluşturulur.
5. Admin iki talebi ayrı ayrı görür.
6. Bir talep onaylanır.
7. Bakiye artışı kontrol edilir.
8. İşlem geçmişinde onay durumu görülür.

## 16.4. Transfer testi

1. Gönderen hesapta yeterli bakiye bulunur.
2. Kullanıcı kendi hesabını seçer.
3. Alıcı hesap ve tutar girilir.
4. Yetersiz bakiye ile transfer denenir.
5. İşlemin reddedildiği kontrol edilir.
6. Yeterli tutarla transfer yapılır.
7. Gönderen ve alıcı bakiyeleri kontrol edilir.
8. Transferin son işlemlerde göründüğü kontrol edilir.

## 16.5. Excel testi

1. A/B/C formatında `.xlsx` dosyası hazırlanır.
2. Admin Excel aktarım alanından dosyayı seçer.
3. Geçerli kayıtların eklendiği kontrol edilir.
4. Aynı e-posta içeren ikinci dosya aktarılır.
5. Duplicate kayıtların atlandığı kontrol edilir.
6. `.numbers` dosyasının doğrudan kabul edilmediği kontrol edilir.

# 17. Projenin Mevcut Durumu

Proje şu anda çalışan bir banka yönetim sistemi prototipidir. Admin ve kullanıcı akışları birbirinden ayrılmış, kullanıcıların yalnızca kendilerine ait verileri görmesi sağlanmış, admin işlemleri denetim geçmişine bağlanmış ve API’ler Swagger üzerinden test edilebilir hale getirilmiştir.

Özellikle aşağıdaki noktalar projenin teknik değerini artırmaktadır:

- Rol bazlı erişim kontrolü
- Admin onay süreci
- JWT ile korunan endpoint’ler
- SQLite üzerinde ilişkisel veri modeli
- Veri doğrulama ve benzersizlik kontrolü
- Excel’den toplu veri aktarımı
- Para yatırma talebinde onay mantığı
- Transaction kullanılarak bakiye güncelleme
- İşlem geçmişi ve audit log
- Kullanıcı tarafında veri izolasyonu

# 18. Gelecekte Eklenebilecek Geliştirmeler

## Kısa vadeli geliştirmeler

- Transfer ekranında hesap ID yerine hesap seçme dropdown’ı
- Alıcı hesap numarası girildiğinde alıcı adını gösterme
- Transfer öncesi onay ekranı
- PDF dekont üretimi
- Kullanıcı işlem geçmişinde tarih filtresi
- Para yatırma taleplerinde açıklama alanı

## Orta vadeli geliştirmeler

- Hesap ekstresi
- Bildirim merkezi
- Destek talebi oluşturma
- E-posta ile şifre sıfırlama
- İki aşamalı doğrulama
- Kullanıcı oturum geçmişi
- Admin için gelişmiş raporlama

## Üretim ortamı için geliştirmeler

- PostgreSQL veya SQL Server’a geçiş
- Migration yönetimi
- Merkezi loglama
- Rate limiting
- Refresh token yapısı
- Güvenlik başlıkları
- CI/CD pipeline
- Otomatik birim ve entegrasyon testleri
- Gerçek ödeme/banka API entegrasyonu

# 19. Sonuç

Bu proje boyunca basit bir müşteri yönetimi fikri, adım adım daha kapsamlı bir banka yönetim sistemine dönüştürülmüştür. Geliştirme sürecinde yalnızca ekran tasarımı değil; veri modeli, yetki, güvenlik, onay, bakiye, hata yönetimi ve denetlenebilirlik gibi yazılım mühendisliği konuları da ele alınmıştır.

Projenin en önemli sonucu, normal kullanıcının kendi finansal verileriyle sınırlı, adminin ise yönetim işlemlerine odaklı olduğu bir yapı kurulmasıdır. Para yatırma talebinin admin onayına tabi tutulması ve bakiyenin doğrudan elle değil, onaylanmış finansal hareket sonucunda artması da sistemi daha gerçekçi hale getirmiştir.

Swagger entegrasyonu ile API katmanı frontend’den bağımsız test edilebilir hale gelmiş; işlem geçmişi ve audit kayıtları ile sistemde gerçekleşen kritik işlemler izlenebilir olmuştur.

Sonuç olarak proje, gerçek banka API’sine bağlı olmayan ancak gerçek bankacılık uygulamalarındaki temel iş akışlarını ve güvenlik yaklaşımını gösteren, sunuma ve teknik değerlendirmeye uygun bir prototip haline gelmiştir.

