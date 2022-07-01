## Project
This prototype project explores alternative methodologies aiming for speed to calculate pixel colors to Hounsfield units set by the user and quality of the render.

Methodologies: [Unity's Burst compiler (v 1.4.1) multithreaded jobs on CPU](https://docs.unity3d.com/Packages/com.unity.burst@0.2-preview.20/manual/index.html) and [Compute Buffers on GPU](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ComputeBuffer.html). 

Rendering scenarios tested for quad meshes (2D-plane equivalent images) and raymarching volumetric cloud. Each scenario is contained in a Unity scene.

Each scene was not extended in full because this is a prototype and as such it was used determine the viability of the approach for our purposes. This project is therefore not supported.

Project collaborator and donor of dicom images: [TBD]. Project currently has no dicom images, we are awaiting for these to be anonymized and will be uploaded soon.

## Project Software
Unity version: Unity 2020.3.36f1, Build Platform PC Standalone

Dicom images data processed with the [Fellow Oak DICOM library](https://github.com/fo-dicom/fo-dicom)

This [library's](https://github.com/fo-dicom/fo-dicom) license: The MIT License (MIT), Copyright (c) Microsoft Corporation: Permission is hereby granted, free of charge, to any person obtaining a copy  of this software and associated documentation files (the "Software"), to deal  in the Software without restriction, including without limitation the rights  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell  copies of the Software, and to permit persons to whom the Software is  furnished to do so, subject to the [following conditions](https://github.com/fo-dicom/fo-dicom/blob/development/License.txt).

## License
This project is licensed under the MIT License. See [LICENSE.txt](https://github.com/sergiosolorzano/Dicom-3DRender/blob/main/LICENSE.md) for more information.

## Features

* Dicom Processing: When the scene is played, progress of the dicom images being processed is displayed in the Inspector:

  <img width="246" alt="Dicom-Images-Processed-Progress" src="https://user-images.githubusercontent.com/24430655/176902397-e3ed3745-2ba0-4c39-95e3-bd66d9aa6ad3.PNG">

* Render options:
  * Axial, Coronal and Sagittal axis are available. Axial axis was the base for our study and it is recommended to explore the Unity project.

  * Render Scenes (Scenes found in unity project folder ..\Assets\Scenes\):

    + QuadMesh_CPU-RGBA_Range_Picker.unity: 3D model renders according to the RGBA (0-255) unit range selected by the user. Multiple study axis (axial, coronal, sagittal) may be rendered at once and aligned.

      https://user-images.githubusercontent.com/24430655/176893082-cbd84e36-e1a6-43e7-a66e-0c1ad1fb441e.mp4
  
  
    + QuadMesh_GPU_CB_Windowing.unity: Quad meshes: 3D model renders according to the Hounsfield unit set on a Window width and Window Center selected by the user. Multiple study axis (axial, coronal, sagittal) may be rendered at once and aligned.
  
      https://user-images.githubusercontent.com/24430655/176882412-39a4156d-2e70-451a-b6c3-ac033135e69a.mp4
  
  
    + VolumetricRender_3DTexture.unity: Volumetric render using 3D Textures with Slicing functionality. Hounsfield units cannot be re-adjusted. Single study axis (axial, coronal, sagittal) at once.
  
      https://user-images.githubusercontent.com/24430655/176886772-4135dc33-e270-4643-8c2f-4b942009eaee.mp4


    + VolumetricRender_3DRenderTexture.unity: Volumetric render using 3D Render Textures with Slicing functionality. Hounsfield units set according to Window width and Window Center selected by the user. Single study axis (axial, coronal, sagittal) at once.

      https://user-images.githubusercontent.com/24430655/176890215-a7bb0a66-8046-4785-87d0-34494c17385b.mp4



    
