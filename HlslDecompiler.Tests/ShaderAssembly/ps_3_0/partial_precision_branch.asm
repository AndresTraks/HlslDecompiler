ps_3_0
def c2, 0, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
dcl_2d s1
texld_pp r0, v0.xy, s0
mov r1.x, c0.x
if_lt -r1.x, c2.x
texld r1, v0.xy, s1
mul_pp r1, r0, r1
else
mul_pp r1, r0, c1
endif
mul oC0, r1, c0
