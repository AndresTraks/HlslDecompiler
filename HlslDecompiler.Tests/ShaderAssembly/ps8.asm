ps_3_0
def c0, 3, 0, -1, 8
dcl_texcoord v0.xw
mul r0.x, c0.x, v0.x
abs oC0.w, r0.x
mad oC0.xyz, v0.xwx, c0.xxy, c0.zzw
