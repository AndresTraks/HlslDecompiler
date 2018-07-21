ps_3_0
def c0, 2, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
dcl_2d s1
texld r0, v0.yxzw, s1
mad r0.xy, r0.xy, c0.xx, v0.yx
texld r0, r0, s0
mov oC0, r0.wzyx
