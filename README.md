# Finansal Bütçe ve Harcama Yönetim Sistemi

Gelir, gider, bütçe, banka ekstresi ve kişisel finans hareketlerini tek bir panel üzerinden yönetmek için geliştirilen web tabanlı finans uygulamasıdır.

## Projenin Amacı

Kullanıcının banka ekstresini sisteme aktarabilmesi, gelir ve giderlerini kategorilere ayırabilmesi, bütçe limitlerini takip edebilmesi ve finansal durumunu raporlarla analiz edebilmesi amaçlanmıştır.

## Temel Özellikler

- Kullanıcı ve yönetici girişi
- JWT tabanlı kimlik doğrulama
- Kullanıcı kayıt ve profil yönetimi
- Gelir ve gider kaydı oluşturma, güncelleme ve silme
- Banka ekstresi içe aktarma
- PDF ve Excel ekstre önizleme
- Ekstre hareketlerini kategori bazında sınıflandırma
- Kişisel anahtar kelime ve kategori kuralları
- Altın, döviz ve kuyumcu işlemlerini yatırım varlığı olarak takip etme
- Spor, eğitim, market, yemek, ulaşım ve benzeri kategoriler
- Bütçe limitleri oluşturma
- Aylık gelir-gider raporları
- Gider dağılımı pasta grafiği
- Banka bakiyesi, eldeki nakit ve toplam varlık takibi
- Borçlarım ve bana ödenecekler için cari hareket takibi
- Yönetici işlem geçmişi ve kullanıcı yönetimi
- Swagger UI ile API test edebilme
- Modern, animasyonlu ve responsive kullanıcı arayüzü

## Kullanılan Teknolojiler

- ASP.NET Core Web API
- .NET 10
- Entity Framework Core
- SQLite
- JWT Authentication
- Swagger / OpenAPI
- HTML5, CSS3 ve JavaScript
- PdfPig ile PDF metin analizi

## Projeyi Çalıştırma

### Gereksinimler

- .NET 10 SDK
- Git

### Kurulum

```bash
git clone -b test https://github.com/meryyem641/BankApi.git
cd BankApi
dotnet restore
dotnet run
```

Uygulama çalıştıktan sonra:

- Web arayüzü: `http://localhost:5076`
- Swagger UI: `http://localhost:5076/swagger`

## Otomatik Demo Verileri

Uygulama ilk kez çalıştırıldığında gerekli tablolar ve varsayılan kategoriler otomatik oluşturulur. Ayrıca örnek yönetici ve kullanıcı hesapları seed mekanizmasıyla eklenir.

İlk çalıştırmada oluşturulan kullanıcı bilgileri terminal ekranına yazdırılır. Parolalar kaynak kodda tutulmaz; güvenlik amacıyla ilk kurulumda otomatik üretilir.

## Swagger Kullanımı

1. `http://localhost:5076/swagger` adresini açın.
2. Önce `/api/v1/auth/login` endpoint’i ile giriş yapın.
3. Dönen JWT token’ı kopyalayın.
4. Swagger ekranındaki **Authorize** butonuna tıklayın.
5. Token’ı girerek korumalı endpoint’leri test edin.

## Ekstre Aktarımı

Kullanıcı, banka tarafından alınan PDF veya Excel ekstresini bütçe bölümünden yükleyebilir. Sistem:

1. Tarih ve işlem bilgilerini okur.
2. Açıklama ve tutar alanlarını ayrıştırır.
3. İşlemleri önizleme ekranında gösterir.
4. Anahtar kelimelere göre kategori önerir.
5. Kullanıcının onayından sonra kayıtları sisteme ekler.

Örneğin `spor`, `fitness`, `pilates`, `yoga` veya `decathlon` ifadeleri bulunan giderler **Spor** kategorisine yönlendirilir.

## Kişisel Kategori Kuralları

Kullanıcı, kendi kategori kurallarını tanımlayabilir. Örneğin:

```text
Anahtar kelime: spor
Kategori: Spor
Tür: Gider
```

Bu kuraldan sonra açıklamasında “spor” geçen işlemler otomatik olarak Spor kategorisine atanır.

## Proje Yapısı

```text
BankApi/
├── Controllers/       API endpoint'leri
├── Data/              Veritabanı bağlantısı ve DbContext
├── Models/            Veri modelleri ve istek modelleri
├── Services/          İş kuralları ve servisler
├── wwwroot/           Web arayüzü, CSS, JavaScript ve görseller
├── docs/              Proje ve staj raporları
├── Program.cs         Uygulama başlangıcı ve veritabanı hazırlığı
└── BankApi.csproj     Proje ve paket tanımları
```

## Güvenlik Notu

Geliştirme ortamındaki JWT anahtarı örnek bir değerdir. Gerçek kullanımda `Jwt:Key` değeri ortam değişkeni veya güvenli bir secret yönetim sistemi üzerinden verilmelidir.

B

