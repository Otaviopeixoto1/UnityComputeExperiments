# Marching Cubes in Unity
<p>
Another use of GPU Driven Rendering in Unity, this time for the Marching Cubes algorithm on the GPU for complex procedural mesh generation. In this project, I have used compute shaders to generate complex meshes efficiently for indirect drawing. The process is optimezed to generate a vertex pool with only unique vertices alongside an index buffer for indexed drawing.</p>

<p>
Check out <a href="https://otaviopeixoto1.github.io/portfolio/GPUMarchingCubes/">my page</a> containing some of the explanation about the method. Also please refer to the useful articles about the subject:</p>

 <ul>
  <li><a href="https://dl.acm.org/doi/10.1145/37402.37422">Lorensen and Cline’s original article</a></li>
  <li><a href="https://developer.nvidia.com/gpugems/gpugems3/part-i-geometry/chapter-1-generating-complex-procedural-terrains-using-gpu">Nvidia's GPU gems 3 Chapter 1. Generating Complex Procedural Terrains Using the GPU</a></li>
  <li><a href="https://developer.nvidia.com/gpugems/gpugems3/part-vi-gpu-computing/chapter-39-parallel-prefix-sum-scan-cuda">nvidia's GPU gems about prefix sum scan algorithm on GPU</a></li>
</ul> 
