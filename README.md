# ContainerTraining
This is a quick guide to walk you through getting up to speed using containers.

In this guide we will walk you through the following

 - Building an asp.net Core WebAPI
 - Putting your WebAPI in a Docker Container
 - Running the container locally
 - publishing the container to Docker Hub
 - publishing the container to ACR (Azure Container Registry)
 - Running the container in Azure using ACI (Azure Container Service)
 - Running the container as part of a cluster using AKS ( Azure Kubernetes Service)
 
We will be pulling together a mixture of hands on labs from the docs and HOLs in this document that will walk you through these steps with commentary. 



## Prerequisites

The first thing you will need to do is to get your machine prepped and ready. 

You will need the following things installed on your system to walk through these exercises.

Azure CLI [Azure CLI Directions](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)

Docker for [Windows](https://docs.docker.com/v17.09/docker-for-windows/install/) or [Mac](https://docs.docker.com/v17.09/docker-for-mac/install/)

[A Dockerhub Account](https://hub.docker.com/) - This is usually created when you install Docker for Mac or Windows and you sign in to the app. 

Kubectl [Full Directions](https://kubernetes.io/docs/tasks/tools/install-kubectl/). --  [Easiest way](https://docs.microsoft.com/en-us/cli/azure/acs/kubernetes?view=azure-cli-latest) Must have Azure CLI installed 

[Postman](https://www.getpostman.com/) for testing and making calls to our API

##### If you are running on Mac or want to use VS Code

[VSCode for Windows, Mac, or Linux](https://code.visualstudio.com/download) or other text editor (Vim, Sublime, etc...) 

C# for Visual Studio Code [https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp]

[.Net Core 2.1 SDK ](https://www.microsoft.com/net/download/all)  for running our asp.net core projects



## Creating an asp.net core WebAPI
The first thing we need to to set up an asp.net core WebAPI to have something to call when we testing out containers.  For our purposes, this needs to be asp.net core since we will be using kubernetes and running linux containers.  If you need to run windows containers and classic ASP.net then you could use Azure Service Fabric (not covered in this training)  [Service Fabric Quick Start]( https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-quickstart-containers)


##### Using the following tutorial to create a simple todo WebAPI app using VS Code  (or just use the one in the repository ToDoV1)



[Create a Web API with ASP.NET Core and Visual Studio Code](https://docs.microsoft.com/en-us/aspnet/core/tutorials/web-api-vsc?view=aspnetcore-2) <span style="color:red"> SKIP THE CALLING FROM JQUERY SECTION</span>

Now that we have created an asp.net core WebApi we want to prepare it for docker.  

##Running your asp.net core WebAPI in Docker

The first thing we want to do is to make some modifications to our project to prepare it for containerization. In our ***program.cs*** file, we need to add the ***.UserUrls()*** to our ***CreateWebHostBuilder*** method. By default, the app will listen to the localhost, ignoring any incoming requests from outside the container.  By adding "http://0.0.0.0:5000" or "http://*:5000" it will allow it to listen outside the container. There are many ways you can do this. This by far is the easiest. 

<pre><code>
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                <b>.UseUrls("http://0.0.0.0:5000")</b>
                .UseStartup<Startup>();
</code></pre>
--

Next, we want to to create a file called **Dockerfile**  (no extension) and place it in the root of your project.  [Dockerfile best practices](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)


There are many images to use as a base images for docker when using ASP.net Core. We are going to be using two of them. Docker 17.05 and higher allows multi-stage builds. 
 
One for building - **microsoft/dotnet:2.1.300-sdk**  
And one for deployment **microsoft/dotnet:2.1.0-aspnetcore-runtime**

The reason for this is we want the smallest possible size for deployment.  The SDK image is **1.73GB** vs **255MB** for the runtime.

Here are all the possible images. 


- [microsoft/dotnet:2.1.0-runtime-deps](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/#1microsoftdotnet210runtimedeps) - use for deploying self-contained deployment apps

- [microsoft/dotnet:2.1.0-runtime](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/#2microsoftdotnet210runtime) - use for deploying .NET Core console apps

- [microsoft/dotnet:2.1.0-aspnetcore-runtime](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/#3microsoftaspnetcore210aspnetcoreruntime) - use for deploying ASP.NET Core apps

- [microsoft/dotnet:2.1.300-sdk](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/#4microsoftdotnet21300sdk) - use for building .NET Core (or ASP.NET Core apps)

You can find out more information about these different builds [here](https://andrewlock.net/exploring-the-net-core-2-1-docker-files-dotnet-runtime-vs-aspnetcore-runtime-vs-sdk/) with a great post by Andrew Lock.  Or the official documentation [Here](https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/net-core-net-framework-containers/official-net-docker-images) 

The first thing we need to add to the Dockerfile we create is the base image. As stated previously, we will be using the build image.  We name it build-env to reference it later in the file. 

<pre><code>
<b>FROM microsoft/dotnet:2.1.300-sdk AS build-env</b>

</code></pre>

Next we set the working directory. (text in **Bold**)  This is where we will be building the app

<pre><code>
FROM microsoft/dotnet:2.1.300-sdk AS build-env
<b>WORKDIR /app</b>

</code></pre>

After that we copy the project file over and use the dotnet cli to call restore
<pre><code>
FROM microsoft/dotnet:2.1.300-sdk AS build-env
WORKDIR /app

<b># copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore</b>
</code></pre>

Then copy everything else and use the dotnet cli on the image to publish a release version of the app.
<pre><code>
FROM microsoft/dotnet:2.1.300-sdk AS build-env
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore
<b>
# copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out</b>
</code></pre>

Now since everything is published we can use the runtime version to create the image. We set the working directory, copy the files from the out directory we created in the other image, and set the ENTRYPOINT (use dotnet cli to run ToDoV1.dll) 

<pre><code>
FROM microsoft/dotnet:2.1.300-sdk AS build-env
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out
<b>
# build runtime image
FROM microsoft/dotnet:2.1.0-aspnetcore-runtime
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "ToDoV1.dll"]
</b>
</code></pre>

--
We also want to create a <b>.dockerignore</b>  (don't forget the '.' in front of the name) this will make sure we keep the image as fast and as small as possible by ignoring files we don't care about. Place it in the root of your project directory (same as Dockerfile). We are excluding the bin and obj folders but you can add anything you don't need here. 
<pre><code>
<b>
bin\
obj\
</b>
</code></pre>
--

Now we want to create our images using docker at the command line.  Open up your terminal (Command Window, PowerShell, Terminal), CD (Change Directory) into the folder that holds your project (and Dockerfile),  and type the following commands

Don't forget the '.' at the end. This will build our image according to our Dockerfile and give it a name of todov1.  It will be tagged as todov1:latest.  If we want our own tag we just add it in the command todov1:testversion or todov1:version1.0

<b> -> docker build -t todov1 . </b>

It should produce something similar to this. You can see it creating the directories, restoring packages, creating the dll, copying files, destroying the build image, etc...
<pre><code>
Daniels-MacBook-Pro-2:ToDoV2AddingDocker danielegan$ docker build -t todov1 .
Sending build context to Docker daemon  1.302MB
Step 1/10 : FROM microsoft/dotnet:2.1.300-sdk AS build-env
 ---> 90a5a2ee9755
Step 2/10 : WORKDIR /app
 ---> Using cache
 ---> b2d282cb0d82
Step 3/10 : COPY *.csproj ./
 ---> 09cf1314b867
Step 4/10 : RUN dotnet restore
 ---> Running in 930012dd0c50
  Restoring packages for /app/ToDoV1.csproj...
  Generating MSBuild file /app/obj/ToDoV1.csproj.nuget.g.props.
  Generating MSBuild file /app/obj/ToDoV1.csproj.nuget.g.targets.
  Restore completed in 1.66 sec for /app/ToDoV1.csproj.
Removing intermediate container 930012dd0c50
 ---> b67933ac170b
Step 5/10 : COPY . ./
 ---> 64255972a9a7
Step 6/10 : RUN dotnet publish -c Release -o out
 ---> Running in 8098ed84f05d
Microsoft (R) Build Engine version 15.7.179.6572 for .NET Core
Copyright (C) Microsoft Corporation. All rights reserved.

  Restoring packages for /app/ToDoV1.csproj...
  Generating MSBuild file /app/obj/ToDoV1.csproj.nuget.g.props.
  Generating MSBuild file /app/obj/ToDoV1.csproj.nuget.g.targets.
  Restore completed in 656.92 ms for /app/ToDoV1.csproj.
  ToDoV1 -> /app/bin/Release/netcoreapp2.1/ToDoV1.dll
    ToDoV1 -> /app/out/
Removing intermediate container 8098ed84f05d
 ---> b92dc13c5e46
Step 7/10 : FROM microsoft/dotnet:2.1.0-aspnetcore-runtime
 ---> 76e0c475bb4c
Step 8/10 : WORKDIR /app
 ---> Using cache
 ---> 9acdd401e590
Step 9/10 : COPY --from=build-env /app/out .
 ---> 30f9a9c5e76a
Step 10/10 : ENTRYPOINT ["dotnet", "todoapp.dll"]
 ---> Running in e70e31f68997
Removing intermediate container e70e31f68997
 ---> 29928295685e
Successfully built 29928295685e
Successfully tagged todov1:latest
</code></pre>

--
Next we can run the container. 

<b> -> docker run -it --rm -p 5000:5000 -e "ASPNETCORE_URLS=http://+:5000" --name To_Do_App todov1</b>

- -it is running it interactively at the command prompt (as opposed to detached)(i interactive t terminal)
- --rm automatically removes the container at exit (to clean up your local environment)
- -p is setting up the port to connect your local port of 5000 to the port 5000 on the container
- -e is setting an environment variable (we dont really need this since we set it in the program.cs file
- --name is of the container
- and lastly, the name you gave it in the previous step. 


If you want to look around the container you can have it give you a bash prompt when you run it.  Normally you can just add /bin/bash  to the end of the command but if you have an entrypoint defined (we do) you have to run the command like this.

<b> --> docker run -it --rm -p 5000:5000  --name ToDo_App --entrypoint /bin/bash todov1 </b>

That will give you a bash prompt right where your files are in the container. The working directory we defined, the "app" folder.

To exit, you will need to type <b>exit</b> at the bash prompt (as opposed to Ctrl + C)

## Uploading to DockerHub
Once you have your docker container running locally, you will want to put it in a repository so that it can be used. You have many options for this and usually it depends on a couple of things. First, is this an image that will be open source and be used as either a base image or a starter image? Or is this a personal/company image that needs to stay private.






If your container is meant to be consumed (ala base image) DockerHub is probably the best place to host your container.  It allows easy access to it and works seamlessly in the Dockerfile.  If your container is a private container then you need to know where they are running. Your containers should be registered in the same cloud you use for hosting them. 

- Azure Container Registry (ACR)
- AWS EC2 Container Registry (ECR)
- Google Container Registry
- IBM Cloud Container Registry
- Hosting your own in the cloud or locally with Docker Registry

In this section, we are going to assume you want the world to see, and use your container, so we are going to host it in DockerHub. 

--
The first thing we need to do is to go to DockerHub https://hub.docker.com and create a repository to store our image. 


![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CreateRepository.png)

Fill out the form with the desired information. 

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CreateRepositoryForm.png)

Now we can tag and push our image from the command line 

You should already be signed in and your username and password should be in keychain(mac) or credential manager(windows) from when you signed into Docker for Windows/Mac.  For more information on securely signing into Docker see [THIS](https://docs.docker.com/engine/reference/commandline/login/) page.


Open up your command line and type the following command (replacing it with your Docker Account Name)

You are using docker to tag the image we created (todov1) to associate it with your docker account.  

<b>-> docker tag todov1 <_YourDockerAccountName_>/todov1</b>

Next we want to push it to our DockerHub account. Run the following command at the command line. 

<b>-> docker push <_YourDockerAccountName_>/todov1 </b>

When it is done you should see something like the following.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/CompletedPush.png)

You can check https://hub.Docker.com to verify it is there.  You now have your Docker Container in DockerHub for the world to use.


If we want to pull the image from  Docker Hub to your local machine, we need to type 

**-> docker pull accountname/imagename:tag.**

If you don’t specify the tag, you are going to pull the image tagged :latest


## Uploading your container to ACR (Azure Container Registry)

Before we can upload our container to the Azure Container Registry (ACR) we need to actually create the registry.  There are three ways to do this:

1. Use the Azure Portal to create the resource group and the registry ([Instructions here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal))
2. Use Powershell ([Instructions Here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-powershell))
3. User the Azure CLI 

We are going to do it using the Azure CLI.  One of the prerequisites before starting this tutorial was to have the Azure ClI installed. In addition to this, we need to also make sure that the version of the Azure CLI that we have is version 2.0.27 or higher.  Type the following command into your terminal.

**-> az --version**

This will give you not only the version of the az cli but also all the command line interfaces that the az cli utilizes. 

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/azversion.png)


 Make sure the it is above 2.0.27.  If not, go to the link in the prereqs and upgrade/install it.
 
#### Log in to az
 
If you have not used the Azure CLI before you will need to log in using the command 

**-> az login**

This will print out the following line with a code to authenticate the azure cli with your Azure subscription.  If you have already done this, you can skip this step. 

**_To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code SOMECODEHERE to authenticate_**

If you want to test if you are signed in you can run the command to list the resource groups in your subscription.

**-> az group list -o table**

This will give you a list (in table format) of all your resource groups.

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/listtables.png)

#### Create resource group and registry

Now that we have confirmed we are logged in to azure from the command line we can very quickly create our resource group and our registry.

To create the resource group type the following command

**-> az group create --name todov1rg --location eastus**

Now we can create our registry.  Just type the following command into the terminal. 

**-> az acr create --resource-group todov1rg --name todov1registry --sku Basic**

We are using the Basic sku for the registry which works well for testing.  There is also **Standard** and **Premium** skus.  [You can read about them here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-skus).

When it returns you should see the follow output. 

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/registrycreate.png)

#### Upload our image to ACR

The first thing we need to do is to log into the ACR that we just created. You can do that with the following command using the name we created in the last step. 

**-> az acr login --name todov1registry**

You should receive a **Login Succeeded** when it is complete. 

To push an image to ACR you need to have it locally.  You could pull it from github and tag it but we already have ours local.  We just need to tag it close to the same way when we uploaded it to Dockerhub.

We need to issue the following command using docker. 

First, just to remind us of the name of the local image, not the one we tagged for Dockerhub, run the following command to see a list of all of our local images.

**-> docker image ls

You want to tag the todov1.  Not the ones that are already tagged for Dockerhub. (Your name will differ)

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/taggedimages.png)


To tag it for ACR run the following fully qualified command. (which includes the .azurecr.io)

**-> docker tag todov1 todov1registry.azurecr.io/todov1** 

remember if you don't add a tag (with a colon like this todov1:beta) to the end of the image name it will be tagged as todov1:latest

Finally, you can push the images using docker push.

**-> docker push todov1registry.azurecr.io/todov1**

When it completes, you should see output similar to the following. 

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/pushsuccess.png)

If you want to see it in the repository, you can run the following command.

**->az acr repository list --name todov1registry --output table**

Or view it on the Azure portal http://portal.azure.com 

![](https://raw.githubusercontent.com/DanielEgan/ContainerTraining/master/images/portalacr.png)

Next we will run it in an Azure Container Instance.

## Running an ACI (Azure Container Instance)s


<pre><code>

</code></pre>











