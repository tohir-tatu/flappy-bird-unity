# Flappy Bird 2D (Unity 6, mobil)

Unity 6 (6000.6.2f1, URP 2D) da yaratilgan klassik Flappy Bird o'yini.

## Boshqaruv
- **Telefon:** ekranga bosish — sakrash
- **Kompyuter:** `Space` yoki sichqoncha chap tugmasi — sakrash

## Ishga tushirish
1. Repo'ni yuklab oling va Unity Hub orqali **Add → Add project from disk** bilan oching.
2. `Assets/FlappyBird/FlappyBird.unity` sahnasini oching.
3. **Play** bosing.

## Tuzilishi
- `Assets/FlappyBird/FlappyBirdGame.cs` — o'yin mantiqi (qush, quvurlar, hisob, rekord)
- `Assets/FlappyBird/TapArea.cs` — ekranga bosishni qabul qiladi
- `Assets/FlappyBird/Editor/FlappyBirdSetup.cs` — menyu: **Flappy Bird → Sahnani qayta yaratish**
- `Assets/FlappyBird/Prefabs/Pipe.prefab` — quvur prefabi
- `Assets/FlappyBird/Sprites`, `Assets/FlappyBird/Audio` — rasmlar va ovozlar

Sozlamalar (tezlik, tortishish, quvurlar orasidagi bo'shliq) `FlappyBirdGame` obyektining Inspector'ida.

## Android build
**File → Build Profiles → Android → Switch Platform → Build**
