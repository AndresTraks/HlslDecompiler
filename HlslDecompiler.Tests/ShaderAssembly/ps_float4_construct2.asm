ps_3_0
def c0, 1, 0, 2, 0
dcl_texcoord v0.xy
dcl_texcoord1 v1.zw
mad oC0, v0.xxxy, c0.xyyx, c0.yyxy
mad oC1, v0.xxxy, c0.yyxx, c0.yxyy
mad oC2, v0.x, c0.yyyx, c0.yxzy
mov oC3.xy, v0.xy
mov oC3.zw, v1.xy
