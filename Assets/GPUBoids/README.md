# GPU Boids in Unity

<p> Developed by Craig Reynolds in 1986, Boids is an algorith that tries to simulate the flocking behaviour of birds. Each boid represents an individual inside the flock, with their movements being determined by <a href="https://en.wikipedia.org/wiki/Boids"> three simple rules</a>: cohesion, alignement and separation </p>

<p>
Check out <a href="https://otaviopeixoto1.github.io/portfolio/GPUBoids/">my page</a> containing some of the explanation about the method. Also please refer to the useful articles about the subject:</p>

 <ul>
  <li><a href="https://www.artstation.com/blogs/degged/Ow6W/compute-shaders-in-unity-boids-simulation-on-gpu-shared-memory">Danil Goshko’s implementation (no acceleration data structure)</a></li>
  <li><a href="https://on-demand.gputechconf.com/gtc/2014/presentations/S4117-fast-fixed-radius-nearest-neighbor-gpu.pdf">Rama C. Hoetzlein's presentation on nearest neighbors search</a></li>
  <li><a href="https://developer.nvidia.com/gpugems/gpugems3/part-vi-gpu-computing/chapter-39-parallel-prefix-sum-scan-cuda">nvidia's GPU gems about prefix sum scan algorithm on GPU</a></li>
</ul> 
