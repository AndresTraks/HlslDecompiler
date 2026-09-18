vs_2_0
def c7, 0.159154937, 0.5, 6.28318548, -3.14159274
def c5, -0.00000155009923, -0.0000217013894, 0.00260416674, 0.000260416680
def c6, -0.0208333340, -0.125, 1, 0.5
dcl_position v0
dcl_texcoord v1
mad r0.x, v0.x, c4.x, c4.y
mad r0.x, r0.x, c7.x, c7.y
frc r0.x, r0.x
mad r0.x, r0.x, c7.z, c7.w
sincos r1.y, r0.x, c5.y, c6.y
mad r0.y, r1.y, c4.z, v0.y
mul r1.x, r1.y, c4.z
mul o1.x, r1.x, c4.w
mov r0.xzw, v0.xzw
dp4 oPos.x, r0, c0
dp4 oPos.y, r0, c1
dp4 oPos.z, r0, c2
dp4 oPos.w, r0, c3
mov o0.xy, v1.xy
