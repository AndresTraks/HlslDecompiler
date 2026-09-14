ps_3_0
def c1, 0, -1, 0.159154937, 0.5
def c2, 6.28318548, -3.14159274, 0, 0
dcl_texcoord v0.xy
dcl_texcoord1 v1.x
dcl_2d s0
texld r0, v0.xy, s0
add r1.x, r0.w, -c0.x
cmp r1, r1.x, c1.x, c1.y
texkill r1
mad r1.x, v1.x, c1.z, c1.w
frc r1.x, r1.x
mad r1.x, r1.x, c2.x, c2.y
sincos r2.xy, r1.xx
mov r1.xy, r2.yx
dsx r1.z, v0.x
dsy r1.w, v0.y
add r1, -r0, r1
mad oC0, r0.w, r1, r0
