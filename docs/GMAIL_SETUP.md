# Anslut Gmail lokalt

Det här steget ansluter ett Gmail-konto till den inloggade JobTrack-användaren. JobTrack verifierar Gmail-kontot och sparar krypterade åtkomst- och uppdateringsnycklar på servern. Import av ansökningsmejl byggs i nästa etapp.

## 1. Skapa ett projekt hos Google

Öppna [Google Cloud Console](https://console.cloud.google.com/), skapa eller välj ett projekt för JobTrack och aktivera **Gmail API** under **APIs & Services → Library**.

## 2. Konfigurera Google Auth Platform

Öppna **Google Auth Platform**:

- **Branding:** ange appnamnet JobTrack och din kontaktadress.
- **Audience:** välj **External** för privat Gmail. Behåll appen i **Testing** under utvecklingen.
- **Test users:** lägg till den privata Gmail-adress du vill ansluta.
- **Data Access:** lägg till `openid`, `https://www.googleapis.com/auth/userinfo.email` och `https://www.googleapis.com/auth/gmail.readonly`.

Läsbehörigheten gäller Gmail, inte enbart jobbmejl. JobTrack begär ingen rätt att skicka, ändra eller radera mejl. De framtida importreglerna behöver därför avgränsa vilka mejl som används.

## 3. Skapa en OAuth-klient

Under **Clients**, skapa en klient av typen **Web application**, exempelvis JobTrack local.

Lägg till exakt följande under **Authorized redirect URIs**:

```text
http://localhost:5250/signin-gmail
```

Det här är ett serverflöde; JavaScript origins behövs inte för koden i det här projektet.

Spara Client ID och Client Secret privat. Lägg inte en nedladdad klientfil i repositoryt och dela inte hemligheten i chatten.

## 4. Spara konfigurationen lokalt

Kör i API-repositoryt:

```powershell
.\scripts\configure-gmail.ps1
```

Skriptet frågar efter Client ID och döljer Client Secret när du skriver. Det sparar konfigurationen via `dotnet user-secrets`, utanför repositoryt. Värdena skrivs inte ut av skriptet eller skickas som kommandoradsargument.

User-secrets är avsett för lokal utveckling och är inte ett produktionsvalv. I drift behöver samma konfigurationsnycklar finnas i tjänstens sekretesshantering:

```text
Email:Gmail:ClientId
Email:Gmail:ClientSecret
Email:Gmail:ApiBaseUrl
Email:Gmail:WebBaseUrl
```

Standard är API `http://localhost:5250` och webb `http://localhost:5173`. Callback är alltid API-adressen följd av `/signin-gmail`.

## 5. Uppdatera och starta

Stäng ett eventuellt API som körs och kör:

```powershell
dotnet ef database update
dotnet run
```

Starta webben med `npm run dev` i webbrepositoryt. Använd **localhost** i det här flödet, inte 127.0.0.1 eller datorns LAN-adress.

## 6. Anslut kontot

1. Logga in med ditt JobTrack-konto på `http://localhost:5173`.
2. Öppna **Mejlkonton** och välj **Anslut Gmail**.
3. Välj Gmail-konto hos Google och godkänn läsbehörigheten.
4. Efter återkomsten ska rätt Gmail-adress finnas under **Anslutna konton**.
5. Öppna **Mejlkonton** via sidhuvudet i mobilen. Samma JobTrack-konto visar samma anslutning.

Det första flödet startas på datorns webbapp. Mobilen kan visa konton och koppla från dem; den startar inte Google-inloggning på en fysisk telefon mot localhost.

## Frånkoppling och viktiga gränser

**Koppla från** kräver bekräftelse, tar bort serverns lagrade nycklar och försöker återkalla behörigheten hos Google. Om Google inte nås tas kopplingen ändå bort i JobTrack; kontrollera då också JobTracks åtkomst i ditt Google-konto. Dina manuella jobbansökningar raderas inte.

- Google kan kräva ny anslutning när testbehörigheter eller refresh tokens upphör. Läsbehörigheten är ett restricted Gmail scope, med ytterligare verifieringskrav inför offentlig lansering.
- HTTPS behövs i drift. Registrerade Google-redirectadresser, API-adress och webb-adress måste stämma överens. Webb och API behöver en driftsättning som fungerar med de befintliga SameSite-sessionerna.
- Bevara serverns `App_Data/keys` och skydda både dem och databasen; de används för att dekryptera lagrade nycklar.
- Ändrad eller återkallad åtkomst hos Google upptäcks när nästa Google-anrop görs. `VerifiedAt` visar tidpunkten då anslutningen senast verifierades, inte en bakgrundskontroll.
- Outlook och automatisk import ingår inte i detta delsteg.

## Kontroller utan riktiga Google-konton

```powershell
dotnet test tests/MailConnections.Tests/MailConnections.Tests.csproj
```

Testerna använder en simulerad Google-backchannel och enbart LocalDB-databasen `JobTrackMailVerification`. De provar PKCE, korrelationsskydd, engångslänkar, sessionsbindning, kryptering, kontoisolering, behörigheter och frånkoppling. Testkonton ligger kvar i testdatabasen; den vanliga JobTrack-databasen används inte.

## Officiella referenser

- [Google: OAuth för webbservrar](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Google: Gmail-scopes](https://developers.google.com/workspace/gmail/api/auth/scopes)
- [Google: verifiering av känsliga behörigheter](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification)
