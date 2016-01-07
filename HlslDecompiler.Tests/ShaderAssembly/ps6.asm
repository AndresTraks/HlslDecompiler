ps_3_0
def c0, -1, 0, 2, 0
dcl_texcoord v0.xyw
abs oC0.w, v0.w
mad oC0.xyz, v0.yxy, c0.xxy, c0.yyz
