ps_2_0
dcl t0.xy
dcl_2d s0
texld_pp r0, t0.xy, s0
mul_pp r1, r0, c0
mad r1.xyz, r1.xyz, c1.xyz, r0.xyz
mov oC0, r1
