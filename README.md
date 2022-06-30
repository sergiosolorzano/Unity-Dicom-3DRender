## Project
This prototype project explores alternative methodologies aiming for speed to calculate pixel colors to Hounsfield units set by the user.

Methodologies: [Unity's Burst compiler (v 1.4.1) multithreaded jobs on CPU](https://docs.unity3d.com/Packages/com.unity.burst@0.2-preview.20/manual/index.html) and [Compute Buffers on GPU](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ComputeBuffer.html). 

Render on quad meshes (2D-plane equivalent images) and raymarching volumetric cloud. Each scenario is contained in a Unity scene.

Each scene was not extended in full because as a quick prototype, it was used determine the viability of the approach for our original purposes. This project is not supported.

## Project Software
Unity version: Unity 2020.3.36f1

Dicom images data processed with the [Fellow Oak DICOM library](https://github.com/fo-dicom/fo-dicom).

Dicom Images donated by [TBD]. **Project currently has no dicom images, we are awaiting for these to be anonymized, to be uploaded soon.**

## License
This project is licensed under the MIT License. See License.txt for more information.

## Features

Render options:
* Axial, Coronal and Sagittal axis are available. Axial axis was the base for our study and it is recommended to explore the project.

* Render Scenes (..\Assets\Scenes\):

  + QuadMesh CPU-Hounsfield_Range_Picker.unity: 3D model renders according to the Hounsfield unit range selected by the user. Multiple study axis (axial, coronal, sagittal) may be rendered at once and aligned.
  + QuadMesh GPU-CB_Windowing.unity: Quad meshes: 3D model renders according to the Hounsfield unit set on a Window width and Window Center selected by the user. Multiple study axis (axial, coronal, sagittal) may be rendered at once and aligned.
  + Volumetric_Render_3DTexture.unity: Volumetric render using 3D Textures with Slicing functionality. Hounsfield units cannot be re-adjusted. Single study axis (axial, coronal, sagittal) at once.
  + Volumetric_Render_3DRenderTexture.unity: Volumetric render using 3D Render Textures with Slicing functionality. Hounsfield units set according to Window width and Window Center selected by the user. Single study axis (axial, coronal, sagittal) at once.

    
