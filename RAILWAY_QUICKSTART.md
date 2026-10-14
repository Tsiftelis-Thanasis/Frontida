# Railway Deployment - Quick Start (5 Minutes)

## 🚀 Fast Track to Production

### Step 1: Connect GitHub (2 min)
1. Go to https://railway.app
2. Sign in with GitHub
3. Click **New Project** → **Deploy from GitHub**
4. Select `frontida4baby` repo
5. Click **Deploy**

Railway auto-detects Dockerfile and starts building.

### Step 2: Add Database (1 min)
1. In Railway dashboard, click **+ New**
2. Select **Database** → **PostgreSQL**
3. Railway auto-creates connection string as `DATABASE_URL`

### Step 3: Set 3 Critical Variables (1 min)
Go to **Variables** tab and set:

```
ASPNETCORE_ENVIRONMENT=Production
DefaultConnection=${DATABASE_URL}
Stripe__SecretKey=sk_live_YOUR_KEY
Stripe__PublishableKey=pk_live_YOUR_KEY
```

Get Stripe keys from: https://dashboard.stripe.com/apikeys

### Step 4: Email (1 min)
```
Email__SmtpHost=smtp.resend.com
Email__SmtpPort=587
Email__SmtpUser=resend
Email__SmtpPass=<Resend API key>
Email__FromAddress=noreply@mail.frontida4all.gr
Email__AdminEmail=admin@frontida4all.gr
Admin__Email=admin@frontida4all.gr
Admin__Password=<strong unique password>
`
`
`

Get the Resend API key from the Resend dashboard. Never commit it.

### Step 5: Deploy! (immediate)
Click the green **Deploy** button

---

## ✅ You're Live!

- Your app is at: `https://projectname.up.railway.app`
- Logs: **Deployments** tab → View Logs
- Auto-updates with every push to `main`

## 🔧 Troubleshooting

**App not starting?**
- Check **Logs** tab for errors
- Verify all env variables are set
- Try **Restart** deployment

**Database connection error?**
- Check PostgreSQL plugin is active
- Verify `DefaultConnection` variable is set
- Look at logs for connection string issues

**Need custom domain?**
- Go to **Settings** → **Networking** → **Add Domain**
- Update DNS CNAME to Railway's value
- Free SSL certificate auto-generated

---

**Questions?** See full guide: [DEPLOYMENT.md](./DEPLOYMENT.md)
