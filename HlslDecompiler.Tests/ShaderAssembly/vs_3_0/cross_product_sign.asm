vs_3_0
dcl_position v0
dcl_texcoord v1
dcl_position o0
dcl_texcoord o1
def c0, 2, 3, 5, 7
crs o0.xyz, v0.xyz, v1.xyz
mov o0.w, c0.w
sgn o1, v1, r5, r6
