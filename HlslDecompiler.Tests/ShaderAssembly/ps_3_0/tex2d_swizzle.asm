ps_3_0
dcl_texcoord v0.xy
dcl_2d s0
add r0.xy, v0.yx, v0.yx
texld r0, r0.xy, s0
mov oC0, r0.wzyx
