ps_3_0
dcl_texcoord v0.xy
dcl_texcoord1 v3.xyz
dcl_color v2
dcl_texcoord2 v1.xyz
dcl_2d s0
nrm r0.xyz, v3.xyz
nrm r1.xyz, v1.xyz
dp3_sat r0.x, r0.xyz, r1.xyz
texld r1, v0.xy, s0
mul r1, r1, v2
mul oC0.xyz, r0.xxx, r1.xyz
mov oC0.w, r1.w
