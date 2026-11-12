ps_3_0
def c1, 0.159154937, 0.5, 6.28318548, -3.14159274
def c2, 1, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.xy, c1.xy
mad_pp r0.x, c0.x, r0.x, r0.y
frc r0.x, r0.x
mad_pp r0.x, r0.x, c1.z, c1.w
sincos r1.xy, r0.xx
mad_pp r0, r1.yxyy, c2.xxyy, c2.yyyx
texld_pp r1, v0.xy, s0
mul oC0, r0, r1
