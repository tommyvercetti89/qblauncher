# QBLauncher Pro

QBLauncher Pro, sık kullandığınız uygulamalara, klasörlere, belgelere, web sitelerine ve sistem araçlarına belirlediğiniz küresel kısayol tuşuyla anında ulaşmanızı sağlayan Windows hızlı başlatıcısıdır.

## Öne Çıkan Özellikler

- **Küresel Kısayol Dinleyici:** Onay verdiğinizde siz uygulamadan çıkana kadar belirlediğiniz tuş kombinasyonunu arka planda dinler.
- **İki Farklı Seçme Görünümü:**
  - **Kare Izgara Modu:** Aktif ekranı buğulu cam arkasına alır ve ortada kare kısayollar sunar.
  - **Klasik Mini Mod:** Arama kutulu, iki sütunlu kompakt pencere görünümü sunar.
- **Seçici Yönetici Açılışı:** PowerShell, Komut İstemi, Görev Yöneticisi ve Kayıt Defteri gibi yönetici yetkisi gerektirebilen araçların üzerine gelindiğinde sağ tarafta oklarla açılan Normal / Yönetici seçenekleri sunar.
- **Sürükle-Bırak Sıralama ve Hızlı Tuşlar:** Ön ayarlar sekmesinde fareyle sürükleyerek veya ok butonlarıyla sıralama yapabilirsiniz; ilk 9 öğe otomatik olarak `1–9` sayı tuşlarına atanır.
- **6 Tema Seçeneği:** Obsidyen Gece, Mermer Beyazı, Lavanta Moru, Yaprak Yeşili, Okyanus Mavisi ve Kehribar Gecesi temaları hem yönetim ekranına hem hızlı seçme ekranına uygulanır.
- **Kalıcı Ayarlar ve Otomatik Başlatma:** Tüm tercihler `qblauncher-config.json` dosyasında (kurulu veya taşınabilir klasör yanında) saklanır. İsteğe bağlı olarak Windows oturum açılışında sessizce arka planda başlatılabilir.

## Kurulum ve Kullanım

Sürümler sayfasından ihtiyacınıza uygun paketi indirebilirsiniz:

- **QBLauncherPro-Setup.exe:** Türkçe kurulum sihirbazı ile bilgisayara kalıcı kurulum yapar ve kurulu klasörde kullanıcı ayarlarını saklar.
- **QBLauncherPro-Portable.exe:** Kurulum gerektirmeyen, bağımsız tek çalıştırılabilir sürümdür.

### Klavye Kısayolları

- **Genel Kısayol (Varsayılan `Alt + Space`):** Hızlı seçme ekranını açar veya kapatır.
- **`1–9` Rakam Tuşları:** Sıradaki öğeyi doğrudan başlatır.
- **`Ok Tuşları` ve `Enter`:** Öğeler arasında gezinir ve seçili öğeyi çalıştırır.
- **`F1`:** Yönetim ve düzenleme penceresini öne getirir.
- **`Esc`:** Hızlı seçme ekranını kapatır.

## Kaynak Koddan Derleme

Gereksinim: `.NET 10 SDK` (Windows x64)

```powershell
dotnet publish ".\csharp-dotnet\QBLauncherPro.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ".\csharp-dotnet\publish"
```

Kurulum paketini derlemek için kök dizindeki `installer.iss` dosyasını Inno Setup 6 ile çalıştırabilirsiniz.

## Geliştirici ve Tasarım

Bu yazılım **tommyvercetti89** tarafından **Antigravity** ile geliştirilmiştir. Arayüz tasarımında **Filee** uygulamasından esinlenilmiştir.
