ps_1_1
tex t0
tex t1
mul_x2 r0.xyz, t0.xyz, t1.xyz
+ mul r0.w, t0.w, v0.w
mul r0.xyz, r0.xyz, v0.xyz
