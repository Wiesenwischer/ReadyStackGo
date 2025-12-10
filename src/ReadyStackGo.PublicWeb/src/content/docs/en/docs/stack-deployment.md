---
title: Stack Deployment
description: Step-by-step guide to deploying a stack with screenshots
---

This guide shows you how to select, configure, and deploy a stack from the catalog. The screenshots illustrate each step in detail.

## Overview

Deploying a stack in ReadyStackGo is done in a few simple steps:

1. Log in to the system
2. Navigate to the Stack Catalog
3. Select a product
4. Configure variables
5. Start deployment
6. Monitor status

---

## Step 1: Login

Open the ReadyStackGo web interface in your browser. You will be greeted with the login screen where you enter your credentials.

![ReadyStackGo Login Page](/images/docs/01-login.png)

- Enter your **username**
- Enter your **password**
- Click **Sign In**

---

## Step 2: Dashboard

After successful login, you will be taken to the dashboard. Here you can see an overview of your environments and active deployments.

![ReadyStackGo Dashboard](/images/docs/02-dashboard.png)

---

## Step 3: Stack Catalog

Navigate to the **Stack Catalog** via the main menu. The catalog shows all available products that you can deploy.

![Stack Catalog with available products](/images/docs/03-catalog.png)

Each product shows:

- **Name** and **Version**
- **Description** of the product
- **Category** for easy filtering
- **Tags** for search

---

## Step 4: Product Details

Click on a product to open the detail page. Here you will find detailed information and available versions.

![Product detail page](/images/docs/04-product-detail.png)

On this page you can:

- Read the full **product description**
- See the included **stacks**
- Select a **version**
- Proceed to deployment with **Deploy**

---

## Step 5: Configure Deployment

On the deploy page, you configure all necessary variables for your deployment. Variables are organized by groups.

![Deploy configuration page](/images/docs/05-deploy-configure.png)

### Stack Name

Enter a unique **Stack Name**. This name is used to identify the deployment and must be unique.

### Configure Variables

Fill in the required variables. Different input fields are displayed depending on the variable type:

- **String:** Simple text field
- **Password:** Masked password field
- **Port:** Port selection with validation (1-65535)
- **Boolean:** Toggle switch
- **Select:** Dropdown selection
- **Connection String:** Builder dialog for database connections

:::tip[.env Import]
Already have a `.env` file with your configuration values? Click **Import .env** in the sidebar to automatically import all matching variables!

Supported formats:
- Lines starting with `#` are treated as comments
- Values can be quoted: `"value"` or `'value'`
- Only variables defined in the manifest are imported
:::

### Start Deployment

Once all required fields are filled, click the **Deploy** button in the sidebar. The deployment will start and you will be redirected to the deployments overview.

---

## Step 6: Monitor Deployments

In the **Deployments** overview, you can see all active and past deployments with their current status.

![Deployments overview](/images/docs/08-deployments-list.png)

For each deployment you see:

- **Stack Name:** The name you assigned
- **Status:** Running, Stopped, Error
- **Services:** Number of containers
- **Environment:** The target environment

---

## Next Steps

After a successful deployment, you can:

- Monitor stack status in real-time
- View container logs
- Stop or restart the stack
- Change variables and redeploy
- Delete the stack

### Further Documentation

- [RSGo Manifest Format](/en/docs/reference/manifest-format/)
- [Variable Types](/en/docs/reference/variable-types/)
