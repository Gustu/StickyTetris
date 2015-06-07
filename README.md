# StickyTetris
Kinect Game

```
After experiencing this same problem for a while, I found the instructions for solving this (TypeInitializer Exception) are incomplete.

For a basic app, you need cvextern.dll, Emgu.CV.dll, Emgu.CV.UI.dll, Emgu.Util.dll in the .EXE's directory.

You need a x86(x64) dir in the .exe directory and inside "x86" dir you need opencv_calib3dXXX.dll, opencv_contribXXX.dll, opencv_coreXXX.dll, opencv_features2dXXX.dll, opencv_highguiXXX.dll, opencv_imgprocXXX.dll, opencv_legacyXXX.dll, opencv_mlXXX.dll, opencv_objectdetectXXX.dll, opencv_videoXXX.dll and cudart32_42_9.dll, npp32_42_9.dll, opencv_flann240.dll

The app will work as soon as you include all of the required DLLs.
```
