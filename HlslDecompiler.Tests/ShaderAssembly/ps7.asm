ps_3_0
def c0, 0, 2, 3, -1
dcl_texcoord v0.xyw
abs oC0.w, v0.w
mad oC0.xyz, v0.xxy, c0.xyy, c0.zww
