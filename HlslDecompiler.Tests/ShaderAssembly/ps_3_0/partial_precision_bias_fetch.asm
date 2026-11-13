ps_3_0
def c1, 1, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
dcl_2d s1
texld_pp r0, v0.xy, s0
mul_pp r0, r0.xyxw, c1.xxyx
texldb r0, r0, s1
mul oC0, r0, c0
