ps_3_0
def c0, 1, 0, 0, 0
dcl_texcoord v0.xyz
mov_sat_pp oC0.xy, v0.xy
mad oC0.zw, v0.zz, c0.xy, c0.yx
