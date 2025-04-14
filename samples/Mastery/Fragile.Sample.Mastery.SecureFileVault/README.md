# Fragile Güvenli Dosya Kasası

Fragile Güvenli Dosya Kasası, kullanıcıların hassas dosyalarını güvenli bir şekilde depolayabilecekleri, erişim denetimi sağlayan ve tüm verilerini şifreleyerek koruyan bir uygulamadır. Bu proje, Fragile kütüphanesinin ustalık seviyesi örneğidir.

## Özellikler

- **Güçlü Şifreleme**: Tüm dosyalar AES-256 algoritması ile şifrelenir
- **Hata Düzeltme**: Reed-Solomon hata düzeltme kodu sayesinde ufak arşiv bozulmalarını onarabilir
- **Sıkıştırma**: Dosyalar Ultra seviyesinde sıkıştırma ile depolanır
- **Metadata Desteği**: Tüm dosyalar için ayrıntılı metadata bilgisi tutulur
- **Kullanıcı Yönetimi**: Farklı erişim seviyelerine sahip kullanıcılar oluşturulabilir
- **Erişim Kontrolü**: Dosyalara yalnızca yetkili kullanıcıların erişimi sağlanır
- **Güvenli Parola Yönetimi**: Kullanıcı şifreleri PBKDF2 ile salt eklenerek güvenli bir şekilde hash'lenir

## Kasa Mimarisi

Güvenli Dosya Kasası, iki temel bileşenden oluşur:

1. **İndeks Dosyası**: Kasadaki tüm dosyaların ve kullanıcıların bilgilerini içeren, şifrelenmiş bir indeks dosyası
2. **Depolama Dosyaları**: Her dosya için ayrı, benzersiz kimliğe sahip, şifrelenmiş arşiv dosyaları

## Güvenlik Özellikleri

- Her dosya, onu ekleyen kullanıcının şifre hash'i ile şifrelenir
- Dosyaların erişim seviyeleri bireysel olarak kontrol edilebilir
- Hassas dosyalar otomatik olarak daha yüksek erişim seviyesi ile işaretlenir
- Tüm şifreleme işlemleri Fragile kütüphanesinin güvenli şifreleme özellikleri kullanılarak gerçekleştirilir
- Checksum doğrulaması sayesinde veri bütünlüğü kontrolü yapılır

## Kullanım Senaryoları

- Kişisel hassas bilgilerin güvenli depolanması
- Ekip içi gizli belgelerin yetki seviyesine göre paylaşımı
- Şifreler, API anahtarları gibi hassas bilgilerin güvenli saklanması
- Uzun süreli arşivleme gerektiren önemli dosyaların korunması

Bu örnek proje, Fragile kütüphanesinin güçlü özelliklerinin gerçek dünya uygulamalarında nasıl kullanılabileceğini göstermektedir. 