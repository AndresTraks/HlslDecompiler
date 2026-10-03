hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_join_phase
dcl_input vicp[4][0].xyz
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_output_siv o5.x, finalQuadVInsideTessFactor
dcl_temps 1
mov r0.xy, l(0, 0, 0, 0)
loop
ige r0.z, r0.y, l(4)
breakc_nz r0.z
dp3 r0.z, vicp[r0.y][0].xyz, vicp[r0.y][0].xyz
sqrt r0.z, r0.z
add r0.x, r0.z, r0.x
iadd r0.y, r0.y, l(1)
endloop
mul o0.x, r0.x, cb0[0].x
mov o1.x, r0.x
mov o2.x, r0.x
mov o3.x, r0.x
mov o4.x, r0.x
mov o5.x, r0.x
ret
