# ContainerTraining

This is a quick guide to walk you through getting up to speed using containers. We will walk you through
the following:

- Building an ASP.NET Core Web API
- Putting your Web API in a Docker Container
- Running the container locally
- Publishing the container to Docker Hub
- Publishing the container to ACR (Azure Container Registry)
- Deploy the container using a web app in azure
- Setting up Continuous Deployment for your container in a Web App
- Running the container in Azure using ACI (Azure Container Service)
- Running the container as part of a cluster using AKS (Azure Kubernetes Service)

We will be pulling together a mixture of hands on labs from the docs and HOLs in this document that will walk you
through these steps with commentary.

## Prerequisites

The first thing you will need to do is to get your machine prepped and ready.

You will need the following things installed on your system to walk through these exercises:

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
- Docker for [Windows](https://docs.docker.com/v17.09/docker-for-windows/install/) or
  [Mac](https://docs.docker.com/v17.09/docker-for-mac/install/)
- [Docker Hub Account](https://hub.docker.com/) - This is usually created when you install Docker for Mac or Windows and
  you sign in to the app.
- Kubectl [Full Directions](https://kubernetes.io/docs/tasks/tools/install-kubectl/). -- [Easiest
  way](https://docs.microsoft.com/en-us/cli/azure/acs/kubernetes?view=azure-cli-latest) Must have Azure CLI installed
- [Postman](https://www.getpostman.com/) for testing and making calls to our API
- If you are running on Mac or want to use VS Code:
  - [VSCode for Windows, Mac, or Linux](https://code.visualstudio.com/download) or other text editor (Vim, Sublime,
    etc...)
  - [C# for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
- [.NET Core 6.0 SDK](https://www.microsoft.com/net/download/all) for running our ASP.NET Core projects

## Creating an ASP.NET Core Web API

The first thing we need to to set up an ASP.NET Core Web API to have something to call when we testing out containers.
For our purposes, this needs to be ASP.NET Core since we will be using kubernetes and running Linux containers. If you
need to run Windows containers and classic ASP.NET then you could use Azure Service Fabric (not covered in this
training) [Service Fabric Quick
Start](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-quickstart-containers)

Using the following tutorial to create a simple todo Web API app using VS Code (or just use the one in the repository
ToDoV1):

- [Create a minimal web API with ASP.NET Core and Visual Studio
  Code](https://docs.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-6.0&tabs=visual-studio-code).

Now that we have created an ASP.NET Core Web API we want to prepare it for Docker.

## Running your ASP.NET Core Web API in Docker

The first thing we want to do is modify our project to prepare it for containerization. In **_Program.cs_**, we need to
use **_UseKestrel()_** on the builder's **_WebHost_**. By default, the app will listen to the localhost, ignoring any
incoming requests from outside the container. By listening to any IP at port 5000, the app can listen outside the
container. There are many ways you can do this. This by far is the easiest, and later we will also _EXPOSE_ the port in
the Dockerfile too to allow it to be accessed in the cloud.

```c#
builder.WebHost.UseKestrel(serverOptions =>
{
  serverOptions.ListenAnyIP(5000); // Listen for incoming HTTP connection on port 5000.
});
```

Next, we want to create a file called **Dockerfile** (no extension) and place it in the root of your project.
[Dockerfile best practices](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/).

There are many images to use as a base images for Docker when using ASP.NET Core. We are going to be using two of them.
Docker 17.05 and higher allows multi-stage builds:

- One for building - **mcr.microsoft.com/dotnet/sdk:6.0**
- And one for deployment **mcr.microsoft.com/dotnet/aspnet:6.0**

The reason for this is we want the smallest possible size for deployment. The SDK image is **1.73GB** vs. **255MB** for
the runtime.

Here are all the possible images:

- [mcr.microsoft.com/dotnet/runtime-deps:6.0](https://hub.docker.com/_/microsoft-dotnet-runtime-deps): Deploy self-contained deployment apps
- [mcr.microsoft.com/dotnet/runtime:6.0](https://hub.docker.com/_/microsoft-dotnet-runtime): Deploy .NET Core console apps
- [mcr.microsoft.com/dotnet/aspnet:6.0](https://hub.docker.com/_/microsoft-dotnet-aspnet): Deploy ASP.NET Core apps
- [mcr.microsoft.com/dotnet/sdk:6.0](https://hub.docker.com/_/microsoft-dotnet-sdk): Build .NET Core (or ASP.NET Core apps)

You can find out more information about these different builds
[here](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/) with
a great post by Andrew Lock. Or the official documentation
[here](https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/net-core-net-framework-containers/official-net-docker-images).

Following the guide to [Dockerize an ASP.NET Core application](https://docs.docker.com/samples/dotnetcore/), the first
thing we need to add to the Dockerfile we create is the base image. As stated previously, we will be using the build
image. We name it build-env to reference it later in the file.

The file itself will only have the code sections that you need to type, in between we explain what each section is for.
Start by typing the following at the top of the file you just created (Dockerfile):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
```

Next we set the working directory and `EXPOSE` port 5000. This is where we will be building the app:

```dockerfile
WORKDIR /app
EXPOSE 5000
```

After that we copy the project file over and use the .NET CLI to call restore:

```dockerfile
# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore
```

Then copy everything else and use the .NET CLI on the image to publish a release version of the app:

```dockerfile
# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out
```

Now since everything is published we can use the runtime version to create the image. We set the working directory, copy
the files from the out directory we created in the other image, and set the ENTRYPOINT (use the .NET CLI to run
ToDoV1.dll)

```dockerfile
# Build runtime image.
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "TodoApi.dll"]
```

In total the **Dockerfile** will look like:

```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
EXPOSE 5000

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "TodoApi.dll"]
```

> **IMPORTANT** You want to make sure that the dll referenced in the last line of your Dockerfile (TodoApi.dll in this
> case) matches what will be produced by your application. Meaning if you named your application something other than
> TodoApi then you will need to change this line. If you don't, you will get a strange error like, "Did
> you mean to run dotnet SDK commands? Please install dotnet SDK," and you won't know what is causing it.

We also want to create a **.dockerignore** (don't forget the '.' in front of the name) this will make sure we keep the
image as fast and as small as possible by ignoring files we don't care about. Place it in the root of your project
directory (same as Dockerfile). We are excluding the bin and obj folders, as well as stuff associated with VS Code and
Git, but you can add anything you don't need packaged in the container.

```dockerignore
.dockerignore
.env
.git
.gitignore
.vs
.vscode
*/bin
*/obj
```

Now we want to create our images using Docker at the command line. Open up your terminal (Command Window, PowerShell,
Terminal) and cd (change directory) into the `TodoApi/` folder, and type the following
commands.

Don't forget the '.' at the end. This will build our image according to our Dockerfile and give it a name of todov1. It
will be tagged as **todov1:latest**. If we want our own tag we just add it in the command like **todov1:testversion** or
**todov1:version1.0**.

```bash
docker build -t todov1 .
```

It should produce something similar to this. Docker will create the directories, restore packages, create the
dll, copy files, destroy the build image, etc...

```bash
#1 [internal] load build definition from Dockerfile
#1 sha256:be300676b517e70ec318ff4e1522160c157e73cbd4eaecc887c18298429cbfeb
#1 transferring dockerfile: 472B done
#1 DONE 0.0s

#2 [internal] load .dockerignore
#2 sha256:190c69b20b7d7bd19fa8b7d74f3e3ecbc5d1930bd8e93a8a2bbb2cf2a09bcc66
#2 transferring context: 105B done
#2 DONE 0.1s

#3 resolve image config for docker.io/docker/dockerfile:1
#3 sha256:ac072d521901222eeef550f52282877f196e16b0247844be9ceb1ccc1eac391d
#3 ...

#4 [auth] docker/dockerfile:pull token for registry-1.docker.io
#4 sha256:2dc314a4a948d5201caad959bc3fedc60d8812245f41172a39d301e7fa9231f9
#4 DONE 0.0s

#3 resolve image config for docker.io/docker/dockerfile:1
#3 sha256:ac072d521901222eeef550f52282877f196e16b0247844be9ceb1ccc1eac391d
#3 DONE 5.1s

#5 docker-image://docker.io/docker/dockerfile:1@sha256:443aab4ca21183e069e7d8b2dc68006594f40bddf1b15bbd83f5137bd93e80e2
#5 sha256:48a83027ce1f5c304cb45d91fac69a612cf40aeee970f6f9794eda714e262e35
#5 CACHED

#6 [internal] load build definition from Dockerfile
#6 sha256:6ef7dbc1de4e3747da2acb63632b1ab854036bea0068f7a4450fc56abc4376eb
#6 DONE 0.0s

#7 [internal] load .dockerignore
#7 sha256:159da6ba1ce91ee649b9b46c194b769bd34749266d3b522a6dddd73f991c3b99
#7 DONE 0.0s

#8 [internal] load metadata for mcr.microsoft.com/dotnet/aspnet:6.0
#8 sha256:ac4494cbca04ddb415c76edcbcc7688784c2a6ea65dd656286c013738aa3b75f
#8 DONE 0.4s

#9 [internal] load metadata for mcr.microsoft.com/dotnet/sdk:6.0
#9 sha256:9eb4f6c3944cfcbfe18b9f1a753c769fc35341309a8d4a21f8937f47e94c712b
#9 DONE 0.4s

#12 [build-env 1/6] FROM mcr.microsoft.com/dotnet/sdk:6.0@sha256:59d5f3cfa61923f035d160c3452f45b8bbbb79222ec3433476b58dfa2553a80f
#12 sha256:0a0b19c5f10ac999161246b63cfbcb6d50a123fc43dcbe2d7e6a686aae1b109c
#12 DONE 0.0s

#10 [stage-1 1/3] FROM mcr.microsoft.com/dotnet/aspnet:6.0@sha256:e0189fa2887805b5344d28deb397ce4cdda24a12e1690a4aaa24ee98c9930891
#10 sha256:0477e2c7a08c58d896bd0daf79b1f1d650e4975957bf01002e91df539ae238ba
#10 DONE 0.0s

#14 [internal] load build context
#14 sha256:70b2b4c80178ec0c5bfd4f0bc750e6294b0c3e971dfae46c66d527b476c24be8
#14 transferring context: 40.43kB 0.1s
#14 transferring context: 9.29MB 0.6s done
#14 DONE 0.6s

#13 [build-env 2/6] WORKDIR /app
#13 sha256:05acf7dc78e229ecdfa0464d9bdfba70ce25016d95856acf1a6b234532c4587e
#13 CACHED

#15 [build-env 3/6] COPY *.csproj ./
#15 sha256:cd17fe6b9a7581f85e0e1c0042db2e70d221dc67a6927d7820da2cd994bfbeef
#15 CACHED

#16 [build-env 4/6] RUN dotnet restore
#16 sha256:c5cee86bf2a7f1b137c6ba94fff800745a0f85892c3628b4f652ce4ae770b86c
#16 CACHED

#17 [build-env 5/6] COPY . ./
#17 sha256:2b0d3e20073e7fddaf62936b3fe19fe1cc4bed8e3651a8d190943e4af86cee5b
#17 DONE 0.2s

#18 [build-env 6/6] RUN dotnet publish -c Release -o out
#18 sha256:505fddea54d3a50218c09b2d5daed79b43c902cc45bb3b337ec89278f48eb18e
#18 0.566 Microsoft (R) Build Engine version 17.2.0+41abc5629 for .NET
#18 0.566 Copyright (C) Microsoft Corporation. All rights reserved.
#18 0.566
#18 1.841   Determining projects to restore...
#18 3.683   Restored /app/TodoApi.csproj (in 843 ms).
#18 8.363   TodoApi -> /app/bin/Release/net6.0/TodoApi.dll
#18 8.478   TodoApi -> /app/out/
#18 DONE 8.6s

#11 [stage-1 2/3] WORKDIR /app
#11 sha256:5790d9d246621300d2780afef58e515532a82e570b8580baf68a70f6b5e8a4cf
#11 CACHED

#19 [stage-1 3/3] COPY --from=build-env /app/out .
#19 sha256:523fdcb93ec048adf029f85134458e7ead7e3a5146224cef071911ac449eff09
#19 DONE 0.2s

#20 exporting to image
#20 sha256:e8c613e07b0b7ff33893b694f7759a10d42e180f2b4dc349fb57dc6b71dcab00
#20 exporting layers
#20 exporting layers 0.2s done
#20 writing image sha256:7e28663f832381c26e1ee7fc058446d2895aa1fe83c91212010859cf45c1bcf9 0.0s done
#20 naming to docker.io/library/todov1 0.0s done
#20 DONE 0.3s

Use 'docker scan' to run Snyk tests against images to find vulnerabilities and learn how to fix them
```

Next we can run the container:

```bash
docker run -it --rm -p 5000:5000 -e "ASPNETCORE_URLS=http://+:5000" --name To_Do_App todov1
```

> **_NOTE:_** Bash users might get an error like "the input device is not a TTY. If you are using mintty, try prefixing the
> command with 'winpty'." If this is the case, use a different terminal like PowerShell or use winpty to run the command:
>
> ```bash
> winpty docker run -it --rm -p 5000:5000 -e "ASPNETCORE_URLS=http://+:5000" --name To_Do_App todov1
> ```
>
> If the above command fixes the error, you might want to create a Bash alias to handle this in the future without
> typing 'winpty':
>
> ```bash
> echo "alias docker='winpty docker'" >> ~/.bashrc
> ```
>
> Now you should be able to run the original Docker command without any issues.

Let's take a closer look at the arguments in the above command:

- **-it** runs it interactively at the command prompt, as opposed to detached (i interactive t terminal)
- **--rm** automatically removes the container at exit (to clean up your local environment)
- **-p** sets up the port to connect your local port of 5000 to the port 5000 on the **container**
- **-e** sets an environment variable (We don't really need this since we set it in **Program.cs**)
- **--name** is the name of the container
- and lastly, the name you gave it in the previous step.

To prove to yourself that the container is running the app on your machine, open a browser and navigate to
<http://localhost:5000/api/todos>.

If you want to look around the container you can have it give you a Bash prompt when you run it. Normally you can just
add /bin/bash to the end of the command but if you have an entrypoint defined (we do) you have to run the command like
this.

```bash
docker run -it --rm -p 5000:5000 --name To_Do_App --entrypoint /bin/bash todov1
```

That will give you a Bash prompt right where your files are in the container. The working directory we defined, the
"app" folder.

To exit, you will need to type **exit** at the Bash prompt (as opposed to Ctrl + C)

## Uploading to Docker Hub

Once you have your docker container running locally, you will want to put it in a repository so that it can be used. You
have many options for this and usually it depends on a couple of things. First, is this an image that will be open
source and be used as either a base image or a starter image? Or is this a personal/company image that needs to stay
private.

If your container is meant to be consumed (ala base image) Docker Hub is probably the best place to host your container.
It allows easy access to it and works seamlessly in the Dockerfile. If your container is a private container then you
need to know where they are running. Your containers should be registered in the same cloud you use for hosting them.

- Azure Container Registry (ACR)
- AWS EC2 Container Registry (ECR)
- Google Container Registry
- IBM Cloud Container Registry
- Hosting your own in the cloud or locally with Docker Registry

In this section, we are going to assume you want the world to see, and use your container, so we are going to host it in
Docker Hub.

-- The first thing we need to do is to go to Docker Hub <https://hub.docker.com> and create a repository to store our
image.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CreateRepository.png)

Fill out the form with the desired information.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CreateRepositoryForm.png)

Now we can tag and push our image from the command line

You should already be signed in and your username and password should be in keychain (Mac) or credential manager
(Windows) from when you signed into Docker for Windows/Mac. For more information on securely signing into Docker see
[THIS](https://docs.docker.com/engine/reference/commandline/login/) page.

Open up your command line and type the following command (replacing it with your Docker Account Name)

You are using docker to tag the image we created (todov1) to associate it with your docker account.

```bash
docker tag todov1 <_YourDockerAccountName_>/todov1
```

Next we want to push it to our Docker Hub account. Run the following command at the command line.

```bash
docker push <_YourDockerAccountName_>/todov1
```

When it is done you should see something like the following.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CompletedPush.png)

You can check <https://hub.docker.com> to verify it is there. You now have your Docker Container in Docker Hub for the
world to use.

If we want to pull the image from Docker Hub to your local machine, we need to type

```bash
docker pull accountname/imagename:tag.
```

If you don’t specify the tag, you are going to pull the image tagged :latest

## Uploading your container to ACR (Azure Container Registry)

Before we can upload our container to the Azure Container Registry (ACR) we need to actually create the registry. There
are three ways to do this:

1. Use the Azure Portal to create the resource group and the registry ([Instructions
   here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal))
2. Use Powershell ([Instructions
   Here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-powershell))
3. User the Azure CLI

We are going to do it using the Azure CLI. One of the prerequisites before starting this tutorial was to have the Azure
ClI installed. In addition to this, we need to also make sure that the version of the Azure CLI that we have is version
2.0.27 or higher. Type the following command into your terminal.

```bash
az --version
```

This will give you not only the version of the az CLI but also all the command line interfaces that the az CLI utilizes.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/azversion.png)

Make sure the it is above 2.0.27. If not, go to the link in the prereqs and upgrade/install it.

#### Log in to az

If you have not used the Azure CLI before you will need to log in using the command

```bash
az login
```

This will print out the following line with a code to authenticate the Azure CLI with your Azure subscription. If you
have already done this, you can skip this step.

**_To sign in, use a web browser to open the page <https://microsoft.com/devicelogin> and enter the code SOMECODEHERE to
authenticate_**

If you want to test if you are signed in you can run the command to list the resource groups in your subscription.

```bash
az group list -o table
```

This will give you a list (in table format) of all your resource groups.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/listtables.png)

#### Create resource group and registry

Now that we have confirmed we are logged in to azure from the command line we can very quickly create our resource group
and our registry.

To create the resource group type the following command

```bash
az group create --name todov1rg --location eastus

```

> Remember this resource group, we will be using it often during this workshop.\*\*

Now we can create our registry. Just type the following command into the terminal.

```bash
az acr create --resource-group todov1rg --name <_YourRegistryName_> --sku Basic
```

The registry name needs to be unique, so use <your email alias><_YourRegistryName_>.

We are using the Basic sku for the registry which works well for testing. There is also **Standard** and **Premium**
skus. [You can read about them here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-skus).

When it returns you should see the follow output.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/registrycreate.png)

#### Upload our image to ACR

The first thing we need to do is to log into the ACR that we just created. You can do that with the following command
using the name we created in the last step.

```bash
az acr login --name <_YourRegistryName_>
```

You should receive a **Login Succeeded** when it is complete.

To push an image to ACR you need to have it locally. You could pull it from Docker Hub and tag it but we already have
ours local. We just need to tag it close to the same way when we uploaded it to Docker Hub.

We need to issue the following command using docker.

First, just to remind us of the name of the local image, not the one we tagged for Docker Hub, run the following command
to see a list of all of our local images.

```bash
docker image ls
```

You want to tag the todov1. Not the ones that are already tagged for Docker Hub. (Your name will differ)

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/taggedimages.png)

To tag it for ACR run the following fully qualified command. (which includes the .azurecr.io)

```bash
docker tag todov1 <_YourRegistryName_>.azurecr.io/todov1
```

remember if you don't add a tag (with a colon like this todov1:beta) to the end of the image name it will be tagged as
todov1:latest

Finally, you can push the images using docker push.

```bash
docker push <_YourRegistryName_>.azurecr.io/todov1
```

When it completes, you should see output similar to the following.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/pushsuccess.png)

If you want to see it in the repository, you can run the following command.

```bash
az acr repository list --name <_YourRegistryName_> --output table
```

Or view it on the Azure portal http://portal.azure.com

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/portalacr.png)

Next, we will deploy to Azure Container Instances

## Deploying your image to ACI (Azure Container Instance)

Now normally you will be doing your deploying headless. Meaning using some sort of CI/CD Pipeline to deploy your
container from ACR to ACI or others. In that case you will want to authenticate using a service principal. You can find
a quickstart [here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-aci), on how to do
it or more information on using service principals
[here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auth-service-principal).

For our image we will enable the admin user on the registry so we can do it manually from the command line interface. To
enable it, you run the following command if we want. (keep in mind we are still logged in to acr).

```bash
az acr update --name <_YourRegistryName_> --admin-enabled true
```

The rest of the section, we are going to again do it visually in the portal.

First we need to go to the portal and click on **Create A Resource** as we did before and search for **Container
Instances**. Once the blade opens, click on **Create**.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci1.png)

From this page, you can see that we can create the instance from either a public image (Like our Docker Hub image) or
from a private image (like our ACR image). We will do a private images.

When you click on the **Private** toggle, you will boxes for :

- Container Image
- Image Registry login server
- Image Registry username
- Image Registry password.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci2.png)

You can get all of these from the registry that you created earlier. To find it, on the left,

- click on Resource Groups
- Click on the resource group you created ( I called it todov1rg )
- Click on the registry you created ( I called it todov1registry )
- On the left of this blade click on **Access Keys** this will give you the information you need for your private
  registry.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci6.png)

In the Configuration section, you want to set the following:

- DNS name label -- The name that will be used, combined with the region you are in, to access your container instance.
- Port - We are going to leave 80 there even though it is not being used so we can add additional ports
- Click on **Open additional ports** and add port 5000

We will leave everything else as is but you can see we can add environment variables and command overrides if needed.
Click **OK** when done

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci3.png)

This will bring up a summary for you to review. You can see that you can download a template to do this in an automated
fashion at a later date. Click on **OK** when done.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci4.png)

When it is deployed, you can view the resource. To access your container, you can use the FQDN address. Also notice that
you have and IP address which means if you would like you could put a load balancer in front of this (something that
Azure Web Does for you )

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci5.png)

Make sure that you add the port and api route **:5000/api/todo**

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/aci7.png)

Next we will run it in an Azure Web App.

## Deploying your image as an Azure Web App

We are not going to be spending time on what an Azure Web App is other than acknowledging that it is a place in azure
that allows you to host websites, apis, and now containers, with the ability to easily scale them, create minor CD/CI,
among other things. We are going to use one as one way to host our simple todo api container.

For this section we are going to move from the command line and use the visual tools at http://portal.azure.com. Once
you sign on to your account, click on the **Create a resource** link and search for web app (or just navigate to it).
Click on where you see **Web App**

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp1.png)

On the next page, simply click on the **Create** button.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp2.png)

When the WebApp blade comes up you will need to do the following :

- Give it a name (must be unique across azure
- Select your subscription
- Create or use a Resource Group (we created **todov1rg** in other exercises)
- Select an **App Service plan/Location** -- Select a region that is in or near your hosted container
- Configure Container
- Select **Single Container**
- Select **Azure Container Registry** -- Although as you can see, you can select others
- Fill in all the dropdowns (nothing in Startup File)
- Click **OK**

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp3.png)

When your resource comes up you should be able to navigate to the url provided. Make sure you add the **/api/todo** to
the end of the url

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp4.png)

You should see the same json returned that we saw locally.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp5.png)

If you do not see the json, you may need to configure Azure to use port 5000. Go to the Application Settings blade.
Click Add new setting. Add an App Setting of PORT and set its value to 5000. Then click Save at the top of the page.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/webapp6.png)

Next, we will set up CD for your container in a web app

## Setting up Continuous Deployment for your container in a Web App

There are multiple ways to enable CI/CD pipelines for your containers. The most robust would be to use Azure Dev Ops and
pipelines to implement gates and checks along the way. For this lab though, we are going to the ACR webhook. Also, we
are only setting up CI/CD for the staging slot. In this simple scenario, we want to be able to swap staging and
production manually once we are confident that the new container works properly.

So to set up the webhook you will need to do the following:

Sign into the Azure portal if you have not done so already.

Navigate to the webapp we created in the previous lab.

Click on Deployments Slots (Or Deployment Slots Preview) and click on Add Slot
![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd1.png)

Create a slot called Staging. Under Configuration Source, select your production slot (it should just say the name of
the webapp) then click OK.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd2.png)

This will clone the existing webapp and add the current container to this slot. Wait for this to complete. When the
stage appears, click on the stage to open up the overview.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd3.png)

As you can see, the URL for this slot is the same as your production webapp with -staging appended to it. If you would
like to verify that it is working correctly, you can click on the URL to open it (don't forget to append it with
/api/todo)

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd4.png)

Once you are done testing, click on container settings for the staging slot you are on (not the main webapp production
slot) Under Continuous Deployment click the ON toggle and select save. This will create a webhook.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd5.png)

Next, navigate to the registry you created (todov1registry) and select Webhooks.  
Click the ellipses (...) at the end of your webhook.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd6.png)

From here you can make configuration changes. For example you can set the scope, which tells the webhook which tagged
image to look for. If no tag is specified then :latest will be selected.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd7.png)

If you would like to set up this same thing with Docker Hub you can follow the directions at the bottom of [This
Page](https://docs.microsoft.com/en-us/azure/app-service/containers/app-service-linux-ci-cd)

If you would like to see how to use Azure DevOps to deploy your container you can look
[Here](https://docs.microsoft.com/en-us/azure/devops/pipelines/apps/cd/deploy-docker-webapp?view=vsts)

Of course, now we want to test to make sure it works right? The first thing we need to do is make some sort of change.
You can make any change you want to the web app but lets make this easy. Go ito the ToDoController.cs file and change
the word Item1 to Item1Changed (or whatever you would like). This is what we have been looking at when we test our
service.

```bash
        public TodoController(TodoContext context)
        {
            _context = context;

            if (_context.TodoItems.Count() == 0)
            {
                _context.TodoItems.Add(new TodoItem { Name = "Item1Changed" });
                _context.SaveChanges();
            }
        }
```

Once we have done this we can use Docker to build the new image and push it to ACR.

Build image(don't forget the '.' at the end) (make sure you are in the directory that your dockerfile is in)

```bash
docker build -t todov1 .
```

Tag image for ACR

```bash
docker tag todov1 todov1registry.azurecr.io/todov1
```

Push to ACR (You may need to log in again to ACR - az acr login --name <YourRegistryName>)

```bash
docker push todov1registry.azurecr.io/todov1
```

When done, your command line will look similar to this (look for errors)

```bash
➜  ToDoV2AddingDocker git:(master) ✗ docker push todov1registry.azurecr.io/todov1
The push refers to repository [todov1registry.azurecr.io/todov1]
ed663896211d: Layer already exists
8ddd1b1e11fa: Layer already exists
f3f4bb43b885: Layer already exists
280a1e8bf3b7: Layer already exists
fd98629870ee: Layer already exists
d626a8ad97a1: Layer already exists
expose: digest: sha256:4be2518323181c8954ac4a22b47ced6e21c1e10da6986332859e152aff5f2e02 size: 1579
f131451deafe: Layer already exists
8ddd1b1e11fa: Layer already exists
f3f4bb43b885: Layer already exists
280a1e8bf3b7: Layer already exists
fd98629870ee: Layer already exists
d626a8ad97a1: Layer already exists
latest: digest: sha256:4ba89a74e3f74fed7e18b88bac053fb070638d4362b8ee96003c57cb89ed8c12 size: 1579
```

If you go to the Webhooks section of your registry, you can check to make sure it succeeded. (200).

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd8.png)

And finally, make a call to the staging URL to confirm that the change you made shows up.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd9.png)

Finally, you can go to the Deployment Slots on your web app and swap images OR go to the Deployment Slots (Preview) and
send a percentage of traffic to the new container.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/cicd10.png)

Next, we will deploy our container in a Kubernetes Cluster.

## Deploying your image in a Kubernetes Cluster

### Setting up the cluster

Now we will deploy our container to the fourth of our options, a Kubernetes cluster. To do this we will be using AKS,
the Microsoft Managed Kubernetes service. We will use a combination of the Azure CLI and the portal to complete our
tasks.

To begin, we first need to make sure that AKS is associated with your Azure subscription. To do that, run the following
command.

```bash
az provider register -n Microsoft.ContainerService
```

When that finishes, we can create the AKS cluster inside the resource group that we have been using for this workshop
**todov1rg** to do this, type the following command.

```bash
az aks create --resource-group todov1rg --name todov1service --node-count 2 --generate-ssh-keys
```

This will begin to create our cluster with two VMs and generate out ssh-keys for us.

This will take a while to spin up so be prepared to wait 5 or 10 minutes. When it completes you will see something
similar to this.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s1.png)

You now have your first kubernetes cluster in Azure.

### Taking a look around

Before we deploy our application to the cluster, lets take a spin around what was created.

If you go to the portal and look at the resource group you created your cluster in **todov1rg**

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s2.png)

you will notice that there is only one item that relates to the cluster in there, the service itself. This is the
pointer to the master node and is managed for you by azure (patching, upgrading, etc...). You don't have access to the
master node.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s3.png)

So where is all the other stuff associated with the service? If you go back to your resource groups, you will notice
that there is a new resource group called MC_todov1rg_todov1service_eastus (MC = Managed Cluster/ original resource
group / name of service / region of service)

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s4.png)

If you click on that resource group you will see all of the other things that were created for your cluster. (The VMs
the disks, Virtual Network, etc...)

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s5.png)

### Working with Kubectl

Kubectl, pronounced "Kube cuttle" or "Kube control" depending on who you ask, is your command line interface to your
kubernetes cluster. We installed this as part of the pre-reqs at the beginning of the lab. If you did not, head back up
there before continuing. We will be using this tool to deploy and manage our applications and services to the nodes we
created. In addition, we are also going to utilizing YAML files to do our deployment. We will not be going deep into
YAML files here, they are a text based file that is used to set configurations (pods, deployments, etc...) in our
cluster. If you would like to learn more about them just search [Yaml files for
beginners](https://www.google.com/search?q=Yaml+Files+for+beginers&oq=Yaml+Files+for+beginers).

So to communicate with our Azure Kubernetes Cluster we need to connect **Kubectl** to our cluster. To do that we need to
execute the following command.

```bash
  az aks get-credentials --resource-group todov1rg --name todov1service
```

You should see a message similar to the one below stating that it Merged as current context

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s6.png)

Once you have this done, you can run kubectl commands against the cluster. Try a few of them out, starting with :

Get all the commands you can run

```bash
kubectl --help
```

Get all the nodes in the cluster

```bash
kubectl get nodes
```

Get Cluster component statuses

```bash
kubectl get cs
```

Get all pods(you don't have any yet)

```bash
kubectl get pods
```

Get all deployments

```bash
kubectl get deployments
```

Feel free to try out other commands.

### Getting Secret to access private ACR

We could of course deploy our image from Docker Hub since there is no authentication needed but in most cases you will
not be doing that. So for us to deploy our ACR image to the Kubernetes Cluster we will need to give Kubernetes access to
the ACR. They typical way an organization would do this would be to use [AAD and Service Principal]
(<https://docs.microsoft.com/en-us/azure/container-registry/container-registry-authentication>) but for this exercise we
will be generating a secret for AKS to use to access ACR.

In the last exercise where we loaded our container to ACI (Azure container instance) we enabled an admin user on our
ACR. We will need that for this exercise. If you did not keep it around you can find it by going to your ACR (Azure
Container Registry) and click on Access Keys.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s6.png)

Either keep that page open or save them for our next command. The command we need to run is the following:

```bash
kubectl create secret docker-registry SECRET_NAME --docker-server=REGISTRY_NAME.azurecr.io --docker-username=USERNAME --docker-password=PASSWORD --docker-email=ANY_VALID_EMAIL
```

SECRET_NAME = todov1secret -- (This is the name you will use in the Yaml file.)

REGISTRY_NAME = todov1registry -- (This is what mine is named, yours may be different)

USERNAME = Copy from last step

PASSWORD = Copy form last step

ANY_VALID_EMAIL = ummm any valid email :)

When you have run this command you will then see the following on the command line.

```bash
secret "todov1secret" created
```

Now we can load our our container into our cluster using the Yaml file. I have created the file for you. You can find it
in this repository called aks.yaml. Before we run it, I am going to break it down into pieces and explain what it does.

> **_IMPORTANT_** ..I DO NOT want you to create your own Yaml file, they are notoriously fickle and easy to mess up by
> not having the correct amount of spaces. Please use the Yaml file (aks.yaml) that is in this repository when running
> it. Below is an explanation as to what you will find in the Yaml file.

First we are telling the cluster that we are creating a deployment called todo

```yaml
apiVersion: apps/v1beta1
kind: Deployment
metadata:
  name: todo
```

Next, we tell it that we want three replicas of the image. The app is named todo (we need that later for the load
balancer)

```yaml
spec:
  replicas: 3
  template:
    metadata:
      labels:
        app: todo
```

We point the the container in ACR (I am using one that I had with and EXPOSE tag. If you leave off the tag it will
select :latest)

```yaml
spec:
  containers:
    - name: todo
      image: todov1registry.azurecr.io/todov1
```

Next, we set the port that we want access to for the app. As you remember we also use expose in the docker file to
expose port 5000. We set a few resource limits.

```yaml
ports:
  - containerPort: 80
resources:
  requests:
    cpu: 250m
  limits:
    cpu: 500m
```

We add the image pull secret that we created in the last step

```yaml
imagePullSecrets:
  - name: todov1secret
```

Finally, we create a load balancer to spread the load between the 3 replicas that we create. We map port 80 to port 5000
to access the api.

```yaml
---
apiVersion: v1
kind: Service
metadata:
  name: todo
spec:
  type: LoadBalancer
  ports:
    - port: 80
      targetPort: 5000
  selector:
    app: todo
```

Finally, we run the yaml file with the following command. (Make sure you are in the same directory as the Yaml file when
you run it) -f denotes filename

```bash
kubectl create -f aks.yaml
```

This will start to deploy your containers, pods, replicas, etc.. Try running the commands we played with before.

Get all the nodes in the cluster

```bash
kubectl get nodes
```

Get Cluster component statuses

```bash
kubectl get cs
```

Get all pods(you don't have any yet)

```bash
kubectl get pods
```

Get all deployments

```bash
kubectl get deployments
```

Get all services

```bash
kubectl get service
```

That last one is the one we need to test our cluster. Once created, the load balancer will have an external port that we
can hit which will then send the call to the container on port 5000.

It will look something like this:

```bash
NAME         TYPE           CLUSTER-IP     EXTERNAL-IP     PORT(S)        AGE
kubernetes   ClusterIP      10.0.0.1       <none>          443/TCP        4h
todo         LoadBalancer   10.0.125.123   168.62.168.84   80:31318/TCP   4m
```

Once you have that External-IP you can test your cluster to see your app working. http://188.62.168.84/api/todo (Yours
will be different)

Finally, if you prefer to visually see the condition of your cluster you can do that through the portal (kind of). In
the Azure portal navigate to the service you created. Remember, this can be found in the todov1rg resource group called
todov1service. On the service's overview tab you will see three options :

- Monitor Container
- View Logs
- View Kubernetes dashboard

Select View Kubernetes Dashboard

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s8.png)

This will open up a blade that shows you how to view the dashboard. You have already done numbers one through three.
Select the copy icon from number four and paste it into your command line to run it.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s9.png)

It will produce something like this on the command line.

```bash
ContainerTraining git:(master) az aks browse --resource-group todov1rg --name todov1service
Merged "todov1service" as current context in /var/folders/y1/8kxz0m9n3v9d8hw8p2nlsmhr0000gn/T/tmpuxlphmzg
Proxy running on http://127.0.0.1:8001/
Press CTRL+C to close the tunnel...
Forwarding from 127.0.0.1:8001 -> 9090
Forwarding from [::1]:8001 -> 9090
Handling connection for 8001
Handling connection for 8001
Handling connection for 8001
Handling connection for 8001
```

It will then launch a browser for you to view your cluster. You should see something like this when it opens. Starting
with Kubernetes v1.7 RBAC (Role Binding Access Control) is enabled by default. This prevents the dashboard from
accessing your cluster.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s10.png)

To learn about how to fix this you can visit this page. [Accessing RBAC enabled Kubernetes
Dashboard](https://unofficialism.info/posts/accessing-rbac-enabled-kubernetes-dashboard/)

As in other parts of this tutorial, there is the way you should do it in production, and the way we are going to do it
here. In practice, you should utilize your service account to give access to the dashboard with tokens which has many
steps.

We are going to grant admin privileges to the Dashboards Service account which will work but might be a security risk.
[see here](https://github.com/kubernetes/dashboard/wiki/Access-control#admin-privileges)

Since this is just a demo for us, we will grant admin privileges with the follow command:

Run this in your command line.

```bash
kubectl create clusterrolebinding kubernetes-dashboard -n kube-system --clusterrole=cluster-admin --serviceaccount=kube-system:kubernetes-dashboard
```

Once this is done, you can try to bring up the dashboard again by running the following (which you had copied earlier
from the portal)

```bash
az aks browse --resource-group todov1rg --name todov1service
```

This will give you access to your dashboard as seen below. You can get all the same information you get from Kubectl but
now in an interface.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/k8s11.png)

From here feel free to play around with your cluster search the Kubectl commands (Kubectl --help) to remove pods, add
pods, check on health, modify your Yaml. Its just a demo so its ok if you break it.

Enjoy!!
