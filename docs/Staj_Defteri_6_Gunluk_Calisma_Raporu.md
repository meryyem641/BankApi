---
title: "Banka Yönetim Sistemi - 6 Günlük Staj Çalışma Raporu"
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

# Proje Tanımı

Staj sürecinde ASP.NET Core ve SQLite kullanılarak banka yönetim sistemi geliştirilmiştir. Projede iki farklı kullanıcı tipi bulunmaktadır: yönetici ve normal kullanıcı. Yönetici; müşteri, kullanıcı, onay, hesap ve işlem kayıtlarını yönetirken normal kullanıcı yalnızca kendi hesabına ve kendi bankacılık işlemlerine erişebilmektedir.

Bu rapor, yapılan çalışmaları altı iş gününe bölerek staj defterine uygun biçimde açıklamaktadır.

# 1. Gün - Proje Analizi ve Temel Sistem Yapısının Oluşturulması

## Yapılan çalışmalar

İlk gün projenin amacı ve kapsamı belirlenmiştir. Banka yönetim sisteminde hangi kullanıcıların bulunacağı, kullanıcıların hangi işlemleri yapabileceği ve yöneticinin hangi yetkilere sahip olacağı planlanmıştır.

Proje için aşağıdaki temel ihtiyaçlar çıkarılmıştır:

- Yönetici ve normal kullanıcı girişi
- Kullanıcı kayıt işlemi
- Yönetici onay mekanizması
- Müşteri kayıtlarının tutulması
- Kullanıcılara müşteri ve hesap bağlantısı yapılması
- Hesap bakiyesi takibi
- Para yatırma talebi oluşturulması
- Yönetici onayından sonra bakiyenin güncellenmesi
- Hesaplar arası para transferi
- İşlem geçmişinin tutulması
- Swagger üzerinden API testlerinin yapılması

## Kullanılan yöntem

İhtiyaçlar belirlendikten sonra sistemin veri modelleri planlanmıştır. Kullanıcı, müşteri, hesap, para yatırma talebi, para transferi ve işlem geçmişi için ayrı tablolar tasarlanmıştır. Böylece her bilginin kendi sorumluluğuna sahip olması ve verilerin birbirine karışmaması sağlanmıştır.

Backend tarafında ASP.NET Core Web API, veri tabanı tarafında SQLite ve Entity Framework Core kullanılmasına karar verilmiştir. Frontend tarafında ise HTML, CSS ve JavaScript ile yönetim paneli hazırlanmıştır.

## Gün sonunda elde edilen sonuç

Projenin kapsamı ve temel mimarisi oluşturulmuş, hangi tablonun ve hangi API işlemlerinin gerekli olduğu belirlenmiştir. Böylece sonraki günlerde yapılacak backend ve frontend çalışmalarının planı hazırlanmıştır.

# 2. Gün - Kullanıcı Kayıt, Giriş ve Yetkilendirme İşlemleri

## Yapılan çalışmalar

İkinci gün kullanıcıların sisteme kayıt olabilmesi ve giriş yapabilmesi sağlanmıştır. Kayıt formuna kullanıcı adı, e-posta, cep telefonu ve şifre alanları eklenmiştir.

Giriş ekranında kullanıcının giriş türünü seçebilmesi için iki farklı seçenek hazırlanmıştır:

- Kullanıcı girişi
- Yönetici girişi

Kullanıcı kayıt olduktan sonra doğrudan sisteme alınmamış, hesabı onay bekleyen durumda tutulmuştur. Yönetici onayı olmadan giriş yapılması engellenmiştir.

## Yetkilendirme mantığı

Kullanıcı giriş yaptığında sistem, giriş yapan kişinin rolünü kontrol etmektedir. Yönetici rolüne sahip kullanıcılar yönetim ekranlarına erişirken normal kullanıcılar yalnızca kendi hesaplarını ve bankacılık işlemlerini görebilmektedir.

JWT tabanlı kimlik doğrulama kullanılarak giriş yapan kullanıcının kimliği güvenli şekilde taşınmıştır. API isteklerinde kullanıcının yetkisi kontrol edilerek yetkisiz işlemler engellenmiştir.

## Yönetici onay sistemi

Yeni kayıt olan kullanıcılar yönetici panelindeki “Onay Bekleyen Kullanıcılar” bölümünde gösterilmiştir. Yönetici kullanıcıyı onayladığında:

1. Kullanıcının onay durumu aktif hale getirilmiştir.
2. Kullanıcı müşteri kaydıyla ilişkilendirilmiştir.
3. Kullanıcı için gerekli hesap bağlantısı oluşturulmuştur.
4. Kullanıcı sisteme giriş yapabilir hale getirilmiştir.

Yönetici reddettiğinde ise kullanıcı sisteme giriş yapamamaktadır.

## Gün sonunda elde edilen sonuç

Kullanıcı kayıt ve giriş akışı tamamlanmış, normal kullanıcı ile yönetici arasındaki yetki farkı oluşturulmuştur. Onaylanmamış kullanıcıların giriş yapamaması sağlanmıştır.

# 3. Gün - Yönetici Paneli ve Müşteri Yönetimi

## Yapılan çalışmalar

Üçüncü gün yönetici paneli geliştirilmiştir. Yönetici panelinde müşteri ekleme, listeleme, güncelleme ve silme işlemleri hazırlanmıştır.

Yönetici aşağıdaki işlemleri yapabilir hale getirilmiştir:

- Yeni müşteri ekleme
- Müşterileri listeleme
- Müşteri adına göre arama
- E-posta adresine göre arama
- Telefon numarasına göre arama
- Müşteri bilgilerini güncelleme
- Müşteri silme
- Kullanıcıları listeleme
- Kullanıcıyı onaylama veya reddetme
- Kullanıcı-müşteri bağlantısı oluşturma ve kaldırma

## Veri doğrulama çalışmaları

Müşteri bilgilerinin hatalı veya tekrarlı kaydedilmemesi için validasyonlar uygulanmıştır. E-posta adresinin benzersiz olması zorunlu tutulmuştur. Aynı e-posta adresiyle ikinci bir müşteri eklenmeye çalışıldığında işlem durdurulmaktadır.

Telefon numarası için ise sistemin gerçek hayat senaryolarına uygun olması amacıyla aynı telefon numarasının farklı kayıtlarda bulunabilmesine izin verilmiştir. Çünkü bazı durumlarda ortak veya kurumsal telefon numaraları kullanılabilir.

Boş ad, boş e-posta veya boş telefon bilgilerinin kaydedilmesi engellenmiştir. Kullanıcıya işlem başarısız olduğunda daha anlaşılır hata mesajları gösterilmiştir.

## Gün sonunda elde edilen sonuç

Yönetici panelinin temel müşteri yönetimi bölümü tamamlanmıştır. Yönetici, müşterileri tek tek ekleyebilmekte ve mevcut müşteri kayıtlarında arama, güncelleme ve silme işlemleri yapabilmektedir.

# 4. Gün - Excel’den Toplu Müşteri Aktarımı ve Sayfalama

## Yapılan çalışmalar

Dördüncü gün çok sayıda müşterinin tek tek girilmesi yerine Excel dosyasından toplu olarak sisteme aktarılması sağlanmıştır. Bu işlem için `.xlsx` dosyalarının okunması ve satırların müşteri kaydına dönüştürülmesi uygulanmıştır.

Dosya formatı sabit tutulmuştur:

- A sütunu: Ad Soyad
- B sütunu: E-posta
- C sütunu: Telefon

Bu yapı sayesinde sistem dosyadaki her satırı bir müşteri olarak algılamaktadır. Dosyanın ilk satırında başlık bulunması durumunda başlık satırı veri olarak kaydedilmemektedir.

## Aktarım sırasında uygulanan kontroller

Excel aktarımı sırasında her satır için aşağıdaki kontroller yapılmıştır:

1. Satırda üç sütun bulunup bulunmadığı kontrol edilmiştir.
2. Ad soyad alanının boş olup olmadığı kontrol edilmiştir.
3. E-posta alanının boş olup olmadığı kontrol edilmiştir.
4. Telefon alanının boş olup olmadığı kontrol edilmiştir.
5. E-posta adresinin daha önce kullanılıp kullanılmadığı kontrol edilmiştir.
6. Hatalı satırlar için kullanıcıya açıklayıcı uyarı verilmiştir.

Dosya aktarımında yalnızca Excel formatı kabul edilerek Numbers gibi doğrudan desteklenmeyen dosyaların neden yüklenemediği anlaşılır hale getirilmiştir. Numbers dosyasının Excel biçiminde dışa aktarılması gerektiği belirlenmiştir.

## Sayfalama ve arama

Çok sayıda müşteri olduğunda tüm müşterilerin tek ekranda gösterilmesi yerine sayfalama uygulanmıştır. Her sayfada 20 müşteri gösterilerek “1. sayfa”, “2. sayfa” şeklinde gezinme sağlanmıştır.

Arama kutusu eklenerek ad, e-posta veya telefon bilgisine göre müşteri bulunması sağlanmıştır. Arama işlemi girilen kelimenin müşteri bilgilerinden herhangi biriyle eşleşmesine göre yapılmaktadır.

## Gün sonunda elde edilen sonuç

Toplu müşteri aktarımı, müşteri arama ve sayfalama işlemleri tamamlanmıştır. Böylece sistem çok sayıda müşteriyle çalışabilecek hale getirilmiştir.

# 5. Gün - Hesap, Para Yatırma ve Para Transferi İşlemleri

## Yapılan çalışmalar

Beşinci gün normal kullanıcı tarafındaki bankacılık işlemleri geliştirilmiştir. Kullanıcıların başkalarının bilgilerini görmemesi için yöneticiye özel müşteri yönetimi bölümleri normal kullanıcı ekranından kaldırılmıştır.

Kullanıcı yalnızca kendisine bağlı hesapları görebilecek şekilde düzenleme yapılmıştır. Kullanıcı sisteme giriş yaptığında hesabı yoksa “Bağlı hesap bulunamadı” mesajı gösterilmektedir.

## Para yatırma talebi mantığı

Gerçek bir bankacılık sistemi olmadığı için bakiyenin doğrudan artırılması yerine onaylı para yatırma süreci tasarlanmıştır:

1. Kullanıcı kendi hesabını seçer.
2. Yatırmak istediği tutarı girer.
3. Para yatırma talebi oluşturur.
4. Talep bekleyen durumda tutulur.
5. Yönetici panelinde talep görüntülenir.
6. Yönetici talebi onaylarsa hesap bakiyesi artırılır.
7. Yönetici talebi reddederse bakiye değişmez.

Bir kullanıcının birden fazla para yatırma talebi oluşturabilmesine izin verilmiştir. Böylece aynı kullanıcı farklı zamanlarda birden fazla ödeme talebi gönderebilmektedir.

## Para transferi mantığı

Para transferi yalnızca hesabında yeterli bakiye bulunan kullanıcı tarafından yapılabilir. Transfer sırasında gönderen hesap, alıcı hesap ve tutar bilgileri kontrol edilmektedir. Bakiye yetersizse işlem reddedilir. Başarılı transferde gönderen hesabın bakiyesi azaltılır, alıcı hesabın bakiyesi artırılır ve işlem kaydı oluşturulur.

## Gün sonunda elde edilen sonuç

Kullanıcı tarafında hesap görüntüleme, para yatırma talebi oluşturma ve para transferi işlemleri hazırlanmıştır. Bakiye değişikliği yönetici onayına bağlanarak sistemin işleyişi daha gerçekçi hale getirilmiştir.

# 6. Gün - İşlem Geçmişi, Swagger, Arayüz ve Son Testler

## Yapılan çalışmalar

Altıncı gün sistemin izlenebilirliği ve kullanıcı arayüzü geliştirilmiştir. Müşteri ekleme, güncelleme ve silme gibi işlemlerin kim tarafından ve ne zaman yapıldığını göstermek için işlem geçmişi bölümü eklenmiştir.

İşlem geçmişinde aşağıdaki bilgiler tutulmaktadır:

- İşlemi yapan kullanıcı
- İşlem türü
- İşlemin açıklaması
- İşlem tarihi ve saati
- İlgili müşteri veya işlem bilgisi

Saat bilgilerinin Türkiye saatine göre gösterilmesi sağlanmıştır.

## Swagger kullanımı

API endpointlerinin görüntülenmesi ve test edilmesi için Swagger arayüzü aktif hale getirilmiştir. Swagger üzerinden müşteri, kullanıcı, hesap, para yatırma ve transfer endpointleri incelenmiştir.

JWT kullanan endpointleri test edebilmek için Swagger üzerinde Authorization alanı yapılandırılmıştır. Kullanıcı girişinden alınan token, “Bearer token” formatında Swagger’a girilerek yetkili istekler gönderilmiştir.

## Arayüz geliştirmeleri

Kullanıcı ve yönetici ekranları birbirinden ayrılmıştır. Yönetici tarafında müşteri, onay ve işlem yönetimi öne çıkarılmış; normal kullanıcı tarafında hesap, bakiye, para yatırma ve transfer işlemleri gösterilmiştir.

Giriş ekranında kullanıcı ve yönetici giriş seçenekleri ayrı kartlar halinde sunulmuştur. Profilim bölümü üst menüden açılacak şekilde düzenlenmiştir. Profilde kullanıcı adı, e-posta, telefon ve rol bilgileri gösterilmiş, kullanıcının kendi e-posta ve telefon bilgilerini güncelleyebilmesi sağlanmıştır.

Giriş sonrasında kullanıcıya kısa süreli “Hoş geldin” mesajı gösterilmiştir. Arayüzde mor ve lacivert renk uyumu kullanılarak banka ve güvenlik temasına uygun görsel bütünlük oluşturulmuştur.

## Son testler

Son gün aşağıdaki testler gerçekleştirilmiştir:

1. Normal kullanıcı kaydı oluşturma
2. Onaysız kullanıcıyla giriş denemesi
3. Yönetici tarafından kullanıcı onaylama
4. Onaylanan kullanıcıyla giriş yapma
5. Profil bilgilerinin görüntülenmesi
6. Profil bilgilerinin güncellenmesi
7. Yönetici tarafından müşteri ekleme
8. Aynı e-posta adresiyle müşteri ekleme denemesi
9. Excel’den müşteri aktarımı
10. Müşteri arama ve sayfalama
11. Para yatırma talebi oluşturma
12. Yönetici tarafından para yatırma talebini onaylama
13. Bakiyenin güncellenmesi
14. Para transferi oluşturma
15. İşlem geçmişinin kontrol edilmesi
16. Swagger üzerinden yetkili endpoint testi

## Gün sonunda elde edilen sonuç

Projenin temel işlevleri tamamlanmış, kullanıcı ve yönetici ekranları ayrılmış, bankacılık işlemleri kontrollü bir akışa bağlanmış ve API endpointleri Swagger üzerinden test edilebilir hale getirilmiştir.

# Genel Değerlendirme

Altı günlük çalışma sonunda banka yönetim sistemi; kullanıcı kaydı, yönetici onayı, müşteri yönetimi, toplu Excel aktarımı, hesap bağlantısı, para yatırma talebi, bakiye güncelleme, para transferi, işlem geçmişi ve Swagger test süreçlerini içeren bir web uygulamasına dönüştürülmüştür.

Proje gerçek para veya gerçek banka altyapısı kullanmayan bir simülasyon sistemidir. Ancak yönetici onayı, bakiye değişiminin kontrollü yapılması, rol bazlı yetkilendirme, veri doğrulama ve işlem geçmişi gibi uygulamalar gerçek sistemlerde kullanılan temel yazılım geliştirme yaklaşımlarını göstermektedir.
