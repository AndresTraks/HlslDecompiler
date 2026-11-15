ps_3_0
dcl_texcoord v0.xy
dcl_texcoord1 v1.xyz
dcl_2d s0
texld r0, v0.xy, s0
mad oC0.xyz, v1.xyz, r0.xxx, c0.xyz
mad oC0.w, v0.x, r0.x, c0.w
