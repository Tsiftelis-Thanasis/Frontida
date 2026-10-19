# Railway Deployment Guide

This guide walks you through deploying frontida4baby to Railway.

## Prerequisites

- GitHub account with the frontida4baby repository
- Railway account (https://railway.app) — free to start
- Domain name (optional, Railway provides a free subdomain)

## Step 1: Create Railway Account & Project

1. Go to https://railway.app
2. Sign up or log in with GitHub
3. Click **"New Project"** → **"Deploy from GitHub"**
4. Select your **frontida4baby** repository
5. Click **"Deploy"**

Railway will automatically detect the Dockerfile and start building.

## Step 2: Configure PostgreSQL Database

1. In Railway dashboard, click **"+ New"** → **"Database"** → **"PostgreSQL"**
2. Railway will create a PostgreSQL instance
3. The connection string will be auto-generated as `DATABASE_URL` environment variable

## Step 3: Set Environment Variables

In Railway dashboard, go to **Variables** tab and set:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_HTTP_PORTS=8080
DefaultConnection=${DATABASE_URL}
Stripe__SecretKey=sk_live_xxxx (get from Stripe dashboard)
Stripe__PublishableKey=pk_live_xxxx (get from Stripe dashboard)
# App email via Resend SMTP (verify host/port/username against Resend's SMTP docs)
Email__SmtpHost=smtp.resend.com
Email__SmtpPort=587
Email__SmtpUser=resend
Email__SmtpPass=<Resend API key>
Email__FromAddress=noreply@mail.frontida4all.gr
Email__AdminEmail=admin@frontida4all.gr
# Seeded admin account, created on first start only
Admin__Email=admin@frontida4all.gr
Admin__Password=<strong unique password>
```

Never commit these values. Set them only in Railway's **Variables** tab.

## Step 4: Run Database Migrations

Railway automatically runs migrations on first deploy, but verify:

1. Go to **Deployments** tab
2. Click on the latest deployment
3. Check **Logs** to see if migrations ran successfully
4. Look for: `Done. Success.` or `No migrations pending`

If migrations didn't run:
```bash
# SSH into Railway container and run manually
railway run dotnet ef database update
```

## Step 5: Configure Custom Domain (Optional)

1. In Railway dashboard, go to **Settings** → **Networking**
2. Click **"Add Domain"**
3. Enter your domain (e.g., `frontida4all.gr`)
4. Update your domain DNS settings to Railway's CNAME
5. Railway provides free SSL certificate automatically

## Step 6: Enable GitHub Auto-Deploy

1. Go to **Settings** → **GitHub**
2. Connect your GitHub repository (if not already connected)
3. Set **Auto Deploy** to **ON** for the `main` branch
4. Now every push to `main` auto-deploys

## Monitoring & Maintenance

### View Logs
- **Deployments** tab → Click a deployment → **View Logs**
- Real-time logs show build and runtime output
- Check for errors, warnings, and performance issues

### Environment Variables
- **Variables** tab → Edit or add new variables
- Changes require a **redeploy**

### Database Backups
1. Go to PostgreSQL plugin → **Backups**
2. Railway auto-backs up daily
3. Download backups for local testing

### Restart Service
1. **Deployments** tab → Click current deployment
2. Click **⋮** (menu) → **Restart**
3. Service will redeploy with current code

## Troubleshooting

### "Connection Refused" Error
- Check `DefaultConnection` variable is set
- Verify PostgreSQL database is running: **Plugins** → **PostgreSQL** → Check status

### "502 Bad Gateway"
- Check application logs: **Deployments** → **View Logs**
- Common causes:
  - App crashed: check error logs
  - Database connection failed
  - Environment variables missing

### "Migrations Failed"
```bash
# SSH into container
railway run bash

# Check database connection
dotnet ef database info

# Run migrations manually
dotnet ef database update
```

### Slow Performance
- Check Railway **Metrics** tab for CPU/Memory usage
- Increase plan if needed (pay-as-you-go)
- Optimize database queries if CPU is high

## Deployment Costs

Railway uses **pay-as-you-go** pricing:
- **$5/month** starting credit
- **$0.000116/hour** per vCPU (roughly $9/month for 1 vCPU)
- **$0.0000116/hour** per GB RAM (roughly $0.95/month per GB)
- **PostgreSQL** included in base pricing

**Estimated monthly cost:** $15-30 for a small app

## Rolling Back

If something breaks:

1. Go to **Deployments** tab
2. Find the previous working deployment
3. Click **⋮** → **Redeploy**
4. Railway re-runs that exact version
5. Current version stays in history for future rollback

## GitHub Actions CI/CD

Once configured, every push to `main` automatically:
1. Builds the Docker image
2. Pushes to Railway
3. Restarts the service
4. Runs migrations
5. Updates your live site

No manual deployment needed after setup!

## Security Checklist

- ✅ Environment variables stored securely (not in code)
- ✅ HTTPS enabled automatically by Railway
- ✅ Database backups enabled
- ✅ Non-root Docker user (UID 1000)
- ✅ Sensitive keys (Stripe, email) in environment variables only
- ✅ Regular backups downloaded and stored safely

## Support

- Railway Docs: https://railway.app/docs
- Railway Discord: https://railway.app/discord
- GitHub Issues: For app-specific problems

---

**You're live!** Your frontida4all application is now deployed and auto-updating with every push to `main`. 🚀
