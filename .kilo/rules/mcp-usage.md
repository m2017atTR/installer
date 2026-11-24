# MCP Kullanım Kuralları

## Genel Prensip
MCP araçlarını **sadece ihtiyaç olduğunda** kullan. Her soruda kullanma.

## Hangi MCP Ne Zaman Kullanılır?

### `memory` (Ortak Hafıza)
**Kullan:**
- Kullanıcı "bunu kaydet", "bunu hatırla", "hafızaya ekle" derse
- Oturumlar arası bilgi paylaşımı gerekirse
- Context/session間 bilgi taşınacaksa
- Uzun vadeli notlar, kararlar, tercihler saklanacaksa
- Sürekli tekrarlanan işler ve hata veren süreçlerde tekrar aynı hataya düşülmemesi gerekli ise

**Kullanma:**
- Basit sorular, tek seferlik işler
- Geçici hesaplamalar

**Senin (kullanıcı) bana nasıl söylersin?**
- `"bunu memory'ye kaydet"` veya `"hafızaya ekle"`
- `"bu kararı not al"`
- `"bu hatayı tekrar etmemek için kaydet"`
- `"context'i memory'de sakla"`

### `sequential-thinking` (Adım Adım Düşünme)
**Kullan:**
- Karmaşık, çok aşamalı problemler
- Planlama, tasarım, debug süreci
- "Bu sorunu adım adım düşünelim" denilirse
- Karar vermek için çok faktör değerlendirilmesi gerekiyorsa

**Kullanma:**
- Basit, tek cevaplı sorular
- Doğrudan bilinen fakt sorgulamaları

### `pulsemcp` (MCP Sunucu Keşfi)
**Kullan:**
- "X için MCP sunucusu var mı?" sorulduğunda
- Yeni entegrasyon/araç aranıyorsa
- Mevcut MCP'leri listelemek için

**Kullanma:**
- Diğer her durumda

## Kullanım Sırası
1. Önce kendi bilgimle cevapla
2. Gerekirse `memory` search (geçmiş context)
3. Çok karmaşıkse `sequential-thinking` başlat
4. Yeni MCP aranıyorsa `pulsemcp`

## Pul-semcp Notu
`pulsemcp` sunucusu **deprecated v0beta API** kullanıyor. Çözüm:
- `pulsemcp-server` paketinin v0.1 API destekli yeni sürümünü bekle
- Veya alternatif: https://github.com/pulsemcp/pulsemcp-server/issues takip et
- Şimdilik manuel web araması (`websearch`) kullan