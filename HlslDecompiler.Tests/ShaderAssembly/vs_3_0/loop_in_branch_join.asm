vs_3_0
def c5, 0, 1, 0.159154937, 0.25
def c6, 6.28318548, -3.14159274, -0.000000252398507, 0.0000247609005
def c7, -0.00138883968, 0.0416666418, -0.5, 0
dcl_position v0
dcl_position o0
if b0
mov r0, v0
loop aL, i0
add r0, r0, c0[aL]
endloop
else
mov r0, v0
endif
mad r1, r0, c5.z, c5.w
frc r1, r1
mad r1, r1, c6.x, c6.y
mul r1, r1, r1
mad r2, r1, c6.z, c6.w
mad r2, r1, r2, c7.x
mad r2, r1, r2, c7.y
mad r2, r1, r2, c7.z
mad r1, r1, r2, c5.y
mad o0, r1, c4, r0
