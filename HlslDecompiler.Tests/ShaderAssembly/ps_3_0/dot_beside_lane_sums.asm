ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
texld r0, v0.xy, s0
dp3 oC0.x, r0.xyz, c0.xyz
mul r0, r0, c0
add oC0.yz, r0.zw, r0.yx
mov oC0.w, r0.w
